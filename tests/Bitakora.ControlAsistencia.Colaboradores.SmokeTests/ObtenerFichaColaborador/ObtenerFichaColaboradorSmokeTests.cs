// Issue #356: smoke tests de ObtenerFichaColaborador, GET
// colaboradores/fichas/{tipoIdentificacion}/{numero}. Function GET read-side sobre la proyeccion
// FichaColaborador (receta N1, MEF-ADR-0034/0035): la primera vista materializada del dominio
// Colaboradores, consultable puntualmente por identificacion y base del flujo de reingreso (por eso
// la consulta puntual INCLUYE no-vigentes, a diferencia de un futuro listado).
//
// Arrange via API, nunca sembrando el event store por fuera de ella: el colaborador se crea con
// POST Colaboradores (#330) y, cuando aplica, se termina su vinculacion con POST
// Colaboradores/Terminaciones (#349) -- los mismos comandos que #356 usa como fuente de eventos
// para la proyeccion.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa FichaColaborador DESPUES de que
// Colaboradores persiste sus eventos. Los casos de exito envuelven la consulta en
// Polling.WaitUntilAsync (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling
// directo en tests": si el timeout se agota es un fallo real (worker no desplegado o proyeccion sin
// registrar en el named store), nunca un skip.
//
// Estos tests quedan ROJOS hasta que el deploy publique ObtenerFichaColaborador en dev: mientras la
// revision anterior siga corriendo, la ruta no existe y el host responde 404 a todo -- el caso 400
// falla y el caso 404 pasa por la razon equivocada (mismo precedente que ObtenerTurnoVigenteSmokeTests
// en ControlHoras). El CI de PR no los ejecuta (solo corre *.Tests); su veredicto real se lee
// despues del deploy.
//
// Formas locales DESACOPLADAS del read model de produccion
// (Bitakora.ControlAsistencia.ReadModels.Colaboradores.FichaColaborador/EtiquetaFicha): el smoke
// test no referencia ReadModels (isla, MEF-ADR-0034 seccion 5) ni el Function App.
// FichaColaboradorRespuestaSmoke replica solo el shape de la respuesta HTTP (DTO propio del
// endpoint, ObtenerFichaColaborador.FichaColaboradorRespuesta), no el read model.
//
// No se repite aqui el detalle de upsert/normalizacion de etiquetas (CA-4), ni el mapeo exhaustivo
// de reingreso (CA-5): esas reglas de negocio de la proyeccion ya las cubre el unit test de
// FichaColaboradorProjection (projection-test-writer). Este smoke test es black-box: solo verifica
// que el endpoint desplegado responde con la vista materializada y que el contrato HTTP propio del
// endpoint (CA-6: centinela oculto, no-vigentes incluidos, bordes 404/400) se cumple contra el
// entorno real.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.ObtenerFichaColaborador;

public class ObtenerFichaColaboradorSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/Colaboradores";
    private const string RutaTerminaciones = "/api/Colaboradores/Terminaciones";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase para las respuestas HTTP), mientras que las formas locales de este
    // archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record EtiquetaFichaSmoke(string Categoria, string Valor);

    private sealed record FichaColaboradorRespuestaSmoke(
        string Id,
        string NombreCompleto,
        string CodigoColaborador,
        DateOnly VigenteDesde,
        DateOnly? VigenteHasta,
        IReadOnlyList<EtiquetaFichaSmoke> Etiquetas,
        IReadOnlyDictionary<string, string> EtiquetasNormalizadas);

    // Numero unico por test -- evita colisiones entre ejecuciones repetidas del smoke test: la
    // identidad del stream (y por lo tanto el Id de la ficha) es Identificacion.ToString()
    // ("CC-<numero>"), no un Guid nuevo por llamada.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Issue #387: codigo URL-safe (unreserved RFC 3986) -- corregido de "[TEST]-" (corchetes
    // fuera del set permitido) a "TEST-" para que el arrange no falle con 400.
    private static string NuevoCodigoColaborador() => $"TEST-{Guid.CreateVersion7()}";

    // Mismo formato que ColaboradorAggregateRoot.ComputarStreamId (separador "-" desde #381),
    // reconstruido localmente: el smoke test no referencia el Function App (Colaboradores.Entities).
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static string Ruta(string tipoIdentificacion, string numero) =>
        $"/api/colaboradores/fichas/{tipoIdentificacion}/{numero}";

    private static object PayloadRegistro(
        string numeroIdentificacion, DateOnly fechaInicio, string codigoColaborador) => new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            primerNombre = "[TEST]",
            segundoNombre = (string?)null,
            primerApellido = "Smoke",
            segundoApellido = (string?)null,
            codigoColaborador,
            fechaInicio
        };

    private static object PayloadTerminacion(string numeroIdentificacion, DateOnly fechaEfectiva) => new
    {
        tipoIdentificacion = TipoIdentificacionCc,
        numeroIdentificacion,
        fechaEfectiva
    };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API.
    private async Task RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, string codigoColaborador, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio, codigoColaborador), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
    }

    // Arrange comun (CA-6, "INCLUYE no-vigentes"): cierra la vinculacion vigente -- via el comando
    // que la origina (#349), nunca sembrando el event store por fuera del API.
    private async Task TerminarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, fechaEfectiva), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1/CA-6 (camino feliz, vinculacion abierta): registrar un colaborador nuevo materializa la
    // ficha con Id, NombreCompleto y CodigoColaborador de la vinculacion, y CA-6 exige que el
    // centinela de vigencia abierta (9999-12-31) jamas salga por la API -- VigenteHasta debe llegar
    // vacio (null), nunca la fecha centinela.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna200ConVigenteHastaVacio_CuandoLaVinculacionEstaAbierta()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 15);
        var codigoColaborador = NuevoCodigoColaborador();

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, codigoColaborador, ct);

        // Act + Assert: reintentar el GET hasta que la proyeccion asincrona materialice la ficha.
        var ruta = Ruta(TipoIdentificacionCc, numeroIdentificacion);
        var respuesta = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadFromJsonAsync<FichaColaboradorRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);
        }, Timeout);

        // Sin assert de NotBeNull: WaitUntilAsync devuelve un valor no nulo o lanza TimeoutException
        // ("el worker no materializo FichaColaborador dentro del timeout"), nunca null.
        respuesta.Id.Should().Be(ComputarStreamId(numeroIdentificacion));
        respuesta.NombreCompleto.Should().Be("[TEST] Smoke");
        respuesta.CodigoColaborador.Should().Be(codigoColaborador);
        respuesta.VigenteDesde.Should().Be(fechaInicio);
        respuesta.VigenteHasta.Should().BeNull(
            "el centinela de vigencia abierta (9999-12-31) jamas debe salir por la API (CA-6)");
        respuesta.Etiquetas.Should().BeEmpty();
        respuesta.EtiquetasNormalizadas.Should().BeEmpty();
    }

    // CA-2/CA-6 (INCLUYE no-vigentes): tras terminar la vinculacion, la consulta puntual sigue
    // respondiendo 200 -- a diferencia de un futuro listado filtrado por vigencia, este endpoint es
    // la base del flujo de reingreso y necesita ver la ficha aunque este cerrada. VigenteHasta debe
    // reflejar la fecha efectiva real de la terminacion, nunca el centinela ni vacio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna200ConVigenteHastaReal_CuandoLaVinculacionEstaTerminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 2, 1);
        var fechaEfectiva = new DateOnly(2026, 6, 30);
        var codigoColaborador = NuevoCodigoColaborador();

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, codigoColaborador, ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectiva, ct);

        // Act + Assert: reintentar hasta que el worker aplique TAMBIEN VinculacionTerminada -- un
        // 200 con VigenteHasta todavia nulo solo significa que la proyeccion aun no proceso el
        // segundo evento del stream.
        var ruta = Ruta(TipoIdentificacionCc, numeroIdentificacion);
        var respuesta = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<FichaColaboradorRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);

            return body?.VigenteHasta is not null ? body : null;
        }, Timeout);

        respuesta.Id.Should().Be(ComputarStreamId(numeroIdentificacion));
        respuesta.VigenteHasta.Should().Be(fechaEfectiva);
    }

    // CA-6: ficha inexistente -> 404 sin body -- distingue el NotFoundResult() del endpoint de un
    // 404 con payload de error, y de la pagina de error del host si la ruta no existiera.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna404SinBody_CuandoLaFichaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        // Numero nunca registrado por ningun test -- no puede tener ficha materializada.
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var response = await _client.GetAsync(Ruta(TipoIdentificacionCc, numeroIdentificacion), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    // CA-6: tipoIdentificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400 --
    // TipoIdentificacion.Desde rechaza y el endpoint traduce a BadRequest (borde HTTP tipado,
    // MEF-ADR-0037).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var response = await _client.GetAsync(Ruta("XX", numeroIdentificacion), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
