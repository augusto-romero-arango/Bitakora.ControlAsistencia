// Issue #376 (MEF-ADR-0043 paso 3): smoke tests del endpoint DELETE
// colaboradores/{id}/etiquetas/{categoria} (retirar la etiqueta de una categoria -- remocion veraz,
// sin payload). Reemplaza el POST Colaboradores/Etiquetas/Retiros (issue #355): la ruta vieja deja
// de existir (CA-6), {id} = Identificacion.ToString() ("CC-<numero>", round-trip con
// Identificacion.Parsear, MEF-ADR-0037), MISMA ruta que AsignarEtiqueta (se distinguen por verbo
// HTTP), y sin body -- RetirarEtiquetaValidator (que validaba el body viejo) se elimino, no hay
// nada que deserializar en ese punto. Molde: TerminarVinculacionSmokeTests/
// IniciarVinculacionSmokeTests -- mismo comando event-sourcing puro sin consumidores downstream
// (CA-ADR-0030): sin ServiceBusFixture, la unica verificacion black-box de los efectos del handler
// es leer mt_events via PostgresFixture.
//
// Arrange: RetirarEtiqueta exige un ColaboradorAggregateRoot existente con la categoria YA
// ASIGNADA -- el arrange de cada test registra el colaborador y asigna (y, cuando aplica, termina
// su vinculacion o inicia una vinculacion nueva, escenario de reingreso issue #378) via los mismos
// comandos que los originan (#330, #349/#379, #378, y AsignarEtiqueta del propio ciclo de vida, ya
// migrado a PUT por este issue), nunca sembrando datos por fuera del API. Issue #379: la
// terminacion ahora exige el {codigo} de la vinculacion en la ruta -- RegistrarColaboradorAsync
// devuelve el codigo (== CodigoColaborador del comando, verificado en
// ColaboradorAggregateRoot.Registrar) para que el arrange lo use como {codigo} al terminar.
//
// Contenido persistido (EtiquetaRetirada, payload plano con solo CategoriaNormalizada -- un campo
// ESCALAR top-level, a diferencia de EtiquetaAsignada): a diferencia de AsignarEtiquetaSmokeTests,
// aqui SI se puede filtrar por (campoJson, valorJson) con el overload estandar de
// PostgresFixture.ExisteEventoAsync/ObtenerEventoAsync, incluso en streams que acumulan mas de un
// evento etiqueta_retirada.
//
// Contrato del endpoint (MEF-ADR-0004 "Respuestas HTTP" y "Estado ya alcanzado"): el evento queda
// confirmado en el event store ANTES de responder, asi que el exito es 204, nunca 202. Una
// categoria SIN etiqueta en la vinculacion vigente -- nunca asignada, transcrita con typo, o
// heredada de una vinculacion anterior tras un reingreso -- tambien responde 204, sin evento nuevo:
// es no-op exitoso, no 409. Quedan en rechazo la vinculacion con terminacion registrada (409, un
// preaviso sin vencer bloquea igual) y el colaborador inexistente (404); un {id} de ruta malformado
// da 400 sin tocar el event store.
//
// Fuera de alcance: una {categoria} de ruta vacia no es validacion de aplicacion sino un segmento
// ausente -- lo resuelve el routing de Azure Functions ("DELETE .../etiquetas/"), no el endpoint.
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.RetirarEtiquetaFunction;

public class RetirarEtiquetaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoEtiquetaAsignada = "etiqueta_asignada";
    private const string TipoEventoEtiquetaRetirada = "etiqueta_retirada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La ausencia de evento es sincrona (sin proyeccion downstream, event-sourcing puro) -- un
    // timeout corto alcanza para probarla sin alargar la suite.
    private static readonly TimeSpan TimeoutAusencia = TimeSpan.FromSeconds(3);

    // Numero unico por test -- evita colisiones entre ejecuciones repetidas del smoke test: la
    // identidad del stream es Identificacion.ToString() ("CC-<numero>"), no un Guid nuevo por
    // llamada, asi que reusar un numero fijo haria que el arrange (RegistrarColaborador) choque con
    // 409 en la segunda corrida. El formato "N" en MAYUSCULAS ya es alfanumerico ASCII, asi que
    // sobrevive intacto a la limpieza del numero (#381) y la llave esperada de abajo coincide con
    // la que arma el backend.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente de la clave de stream (MEF-ADR-0002): se recompone aqui a mano, no se
    // deriva de Identificacion.ToString(), para que un cambio de formato en el VO no se auto-valide.
    // Separador "-" desde el issue #381. Es EXACTAMENTE el mismo valor que el {id} de ruta del
    // endpoint (round-trip con Identificacion.Parsear, issue #376): se reusa para ambos fines.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // Rutas del ciclo de vida migradas por el issue #376: AsignarEtiqueta (PUT, arrange) y
    // RetirarEtiqueta (DELETE, bajo prueba) comparten el mismo template de ruta.
    private static string RutaEtiqueta(string id, string categoria) =>
        $"/api/colaboradores/{id}/etiquetas/{Uri.EscapeDataString(categoria)}";

    private static object PayloadRegistro(string numeroIdentificacion, DateOnly fechaInicio, string codigoColaborador) => new
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

    // Body reducido a los 2 campos que no se derivan de la ruta (issue #378): CodigoColaborador +
    // FechaInicio.
    private static object PayloadIniciarVinculacion(string codigoColaborador, DateOnly fechaInicio) => new
    {
        codigoColaborador,
        fechaInicio
    };

    // Body reducido de AsignarEtiqueta (issue #376, arrange de este archivo): solo Valor.
    private static object PayloadValor(string valor) => new { valor };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API. Devuelve el codigo de la
    // vinculacion inicial (== CodigoColaborador del comando, verificado en
    // ColaboradorAggregateRoot.Registrar) para que el arrange lo use como {codigo} de ruta al
    // terminar (issue #379).
    private async Task<string> RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var codigo = NuevoCodigoColaborador();

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio, codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");

        return codigo;
    }

    // Arrange comun: asigna la etiqueta que luego se intenta retirar -- via el comando que la
    // origina (AsignarEtiqueta, ya migrado a PUT colaboradores/{id}/etiquetas/{categoria} por este
    // mismo issue), nunca sembrando el event store por fuera del API.
    private async Task AsignarEtiquetaAsync(
        string id, string categoria, string valor, CancellationToken ct)
    {
        var response = await _client.PutAsJsonAsync(RutaEtiqueta(id, categoria), PayloadValor(valor), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarEtiqueta funcione");
    }

    // Arrange comun (CA-5): cierra la vinculacion vigente -- via el comando que la origina
    // (#349/#379), nunca sembrando el event store por fuera del API. Issue #379: la ruta gano el
    // {codigo} -- ya no es "/api/Colaboradores/Terminaciones" con identificacion en el body.
    private async Task TerminarVinculacionAsync(
        string id, string codigo, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones/{codigo}:terminar",
            new { fechaEfectiva },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    // Arrange comun (CA-6): inicia una vinculacion nueva sobre el colaborador tras una terminacion
    // -- escenario de negocio de reingreso -- via el comando que lo origina (issue #378, reemplaza
    // a ReingresarColaborador #350), nunca sembrando el event store por fuera del API.
    private async Task IniciarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{ComputarStreamId(numeroIdentificacion)}/vinculaciones",
            PayloadIniciarVinculacion(NuevoCodigoColaborador(), fechaInicio),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que IniciarVinculacion funcione");
    }

    private Task<HttpResponseMessage> RetirarEtiquetaAsync(
        string id, string categoria, CancellationToken ct) =>
        _client.DeleteAsync(RutaEtiqueta(id, categoria), ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Camino feliz: retirar por una forma de la URL distinta de la que se asigno ("área" retira lo
    // asignado como "Area", misma categoria normalizada) -> 204 y el stream recibe etiqueta_retirada
    // con la categoria normalizada. Sin Service Bus (event-sourcing puro): mt_events es la unica
    // ventana black-box a lo que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204YPersisteEtiquetaRetirada_CuandoLaRutaLlegaConCategoriaEnOtraForma()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);
        await AsignarEtiquetaAsync(id, "Area", "Ventas", ct);

        var response = await RetirarEtiquetaAsync(id, "área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, Timeout,
            campoJson: "CategoriaNormalizada", valorJson: "area");

        existe.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaRetirada} con CategoriaNormalizada 'area' deberia existir en el stream {id}");
    }

    // Retirar una categoria que nunca se asigno -> 204, sin evento nuevo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204SinEvento_CuandoCategoriaNoExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, TimeoutAusencia);
        existeRetiro.Should().BeFalse(
            "retirar una categoria sin etiqueta es un no-op exitoso -- no deberia persistir etiqueta_retirada");
    }

    // Repetir el MISMO DELETE tras uno exitoso: el estado ya alcanzado canonico (MEF-ADR-0004).
    // El conteo de etiqueta_retirada se mantiene en 1 -- el segundo llamado no agrega nada al
    // stream. Los demas no-op parten de una categoria que nunca estuvo en la vinculacion vigente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204SinEvento_CuandoLaCategoriaYaFueRetirada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 3, 1), ct);
        await AsignarEtiquetaAsync(id, "Area", "Ventas", ct);

        var primerRetiro = await RetirarEtiquetaAsync(id, "Área", ct);
        primerRetiro.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var segundoRetiro = await RetirarEtiquetaAsync(id, "Área", ct);

        segundoRetiro.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await segundoRetiro.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var retiros = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada);
        retiros.Should().Be(1,
            "el segundo retiro es un no-op exitoso -- no deberia agregar un etiqueta_retirada mas");
    }

    // "Aera" no es "Area": categorias distintas normalizadas, asi que un error de transcripcion NO
    // aflora -> 204 igual, ninguna etiqueta_retirada nueva, y la etiqueta existente ("Area") queda
    // intacta (etiqueta_asignada sigue en 1). Decision deliberada, no un descuido.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204SinEvento_CuandoHayUnErrorDeTranscripcionEnLaCategoriaDeLaRuta()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 5), ct);
        await AsignarEtiquetaAsync(id, "Area", "Ventas", ct);

        var response = await RetirarEtiquetaAsync(id, "Aera", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, TimeoutAusencia);
        existeRetiro.Should().BeFalse(
            "un error de transcripcion en la categoria no deberia persistir un etiqueta_retirada");

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada);
        asignaciones.Should().Be(1,
            "la etiqueta original ('Area') deberia quedar intacta -- el no-op no la toca");
    }

    // Regla estricta de apertura: la ULTIMA vinculacion tiene terminacion registrada -> 409.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 10), ct);
        await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 6, 1), ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Preaviso no vencido: un preaviso con fecha futura ya registrado bloquea igual -- las
    // etiquetas describen la relacion laboral ACTIVA, sin importar si la fecha efectiva ya paso.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoTerminacionEsUnPreavisoConFechaFutura()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);
        var fechaPreavisoFutura = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(id, codigo, fechaPreavisoFutura, ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // El reingreso nace limpio: la etiqueta pertenecia a la vinculacion ANTERIOR (congelada tras la
    // terminacion) y la vigente no la hereda, asi que retirarla es un no-op exitoso.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna204SinEvento_CuandoEtiquetaPerteneceALaVinculacionAnteriorTrasReingreso()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 10), ct);
        await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 6, 1), ct);
        await IniciarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, TimeoutAusencia);
        existeRetiro.Should().BeFalse(
            "la etiqueta de la vinculacion anterior no existe en la vigente: el retiro es un no-op, sin evento nuevo");
    }

    // Colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado
        var id = ComputarStreamId(numeroIdentificacion);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // {id} de ruta sin guion -> 400, con Identificacion.Parsear como unico punto de traduccion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;
        var idSinGuion = NuevoNumeroIdentificacion(); // p.ej. "3F2A0C..." sin "CC-" adelante

        var response = await RetirarEtiquetaAsync(idSinGuion, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Tipo de identificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoTipoDeLaIdentificacionEnLaRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = $"XX-{NuevoNumeroIdentificacion()}";

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Numero vacio tras el guion del {id} de ruta -> 400 (Identificacion.Crear lo rechaza tras la
    // limpieza).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoNumeroDeLaIdentificacionEnLaRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        const string idConNumeroVacio = "CC-";

        var response = await RetirarEtiquetaAsync(idConNumeroVacio, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
