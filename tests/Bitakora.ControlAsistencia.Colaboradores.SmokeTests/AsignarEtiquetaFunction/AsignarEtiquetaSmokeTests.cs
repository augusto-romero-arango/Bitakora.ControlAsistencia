using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.AsignarEtiquetaFunction;

public class AsignarEtiquetaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoEtiquetaAsignada = "etiqueta_asignada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Segunda lectura de un evento que ExisteEventoAsync ya espero: no hay nada mas que esperar.
    private static readonly TimeSpan TimeoutLecturaConfirmada = TimeSpan.FromSeconds(5);

    // La Etiqueta persistida se compara por valor con la serializacion real, nunca contra el texto jsonb.
    // Solo sirve si el stream tiene UN etiqueta_asignada; con dos (sobrescritura, reingreso) se cuenta.
    private static readonly JsonSerializerOptions OpcionesMarten =
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // Unico por test: el stream se llama por la identificacion, asi que repetir un numero choca con 409
    // en la segunda corrida. "N" en mayusculas sobrevive a la limpieza del numero y la llave coincide.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente (MEF-ADR-0002): se recompone a mano, no se deriva de Identificacion.ToString(),
    // para que un cambio de formato en el VO no se auto-valide. Coincide con el {id} de ruta y se reusa.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // La categoria puede traer tildes ("Área") y se escapa; el {id} ya es URL-safe por construccion.
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

    private static object PayloadIniciarVinculacion(string codigoColaborador, DateOnly fechaInicio) => new
    {
        codigoColaborador,
        fechaInicio
    };

    private static object PayloadValor(string valor) => new { valor };

    // Devuelve el codigo de la vinculacion inicial (== CodigoColaborador, ver
    // ColaboradorAggregateRoot.Registrar), que el arrange usa como {codigo} al terminar.
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

    private Task<HttpResponseMessage> AsignarEtiquetaAsync(
        string id, string categoria, string valor, CancellationToken ct) =>
        _client.PutAsJsonAsync(RutaEtiqueta(id, categoria), PayloadValor(valor), ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna204YPersisteEtiquetaAsignada_CuandoCategoriaEsNueva()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaAsignada} deberia existir en el stream {id}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada, TimeoutLecturaConfirmada);

        var etiquetaPersistida = eventoPersistido.GetProperty("Etiqueta").Deserialize<Etiqueta>(OpcionesMarten);

        etiquetaPersistida.Should().Be(Etiqueta.Crear("Área", "Tecnología"));
    }

    // Que el diccionario rehidratado guarde un solo valor por categoria lo cubre
    // AsignarEtiquetaCommandHandlerTests; aqui solo el efecto black-box: otro evento.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna204YAgregaOtroEvento_CuandoLaRutaLlegaConCategoriaEnOtraFormaYValorDistinto()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 20), ct);

        var primeraAsignacion = await AsignarEtiquetaAsync(id, "area", "Medellín", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraEtiqueta = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada, Timeout);
        existePrimeraEtiqueta.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaAsignada} de la primera asignacion deberia estar en el stream {id}");

        var segundaAsignacion = await AsignarEtiquetaAsync(id, "Área", "Bogotá", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada);

        asignaciones.Should().Be(2,
            "PUT .../etiquetas/Área y .../etiquetas/area deberian direccionar la misma etiqueta (CA-4): sobrescribir con un valor distinto agrega un evento nuevo");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna204SinNuevoEvento_CuandoEtiquetaEsIdenticaPorValorALaExistente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 25), ct);

        var primeraAsignacion = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraEtiqueta = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada, Timeout);
        existePrimeraEtiqueta.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaAsignada} de la primera asignacion deberia estar en el stream {id}");

        // Misma etiqueta por valor, con otra combinacion de mayusculas/tildes en la ruta y el body.
        var segundaAsignacion = await AsignarEtiquetaAsync(id, "area", "tecnologia", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada);

        asignaciones.Should().Be(1,
            "una etiqueta identica por valor a la existente no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna409_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);
        await TerminarVinculacionAsync(
            ComputarStreamId(numeroIdentificacion), codigo, new DateOnly(2026, 5, 1), ct);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Un preaviso futuro bloquea igual: las etiquetas describen la relacion laboral ACTIVA.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna409_CuandoTerminacionEsUnPreavisoConFechaFutura()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);
        var fechaPreavisoFutura = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(ComputarStreamId(numeroIdentificacion), codigo, fechaPreavisoFutura, ct);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // La vinculacion del reingreso no hereda las etiquetas de la anterior. El stream queda con dos
    // etiqueta_asignada, por eso se verifica por conteo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna204_CuandoVinculacionEsUnReingresoTrasTerminacionConEtiquetasPrevias()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 10), ct);

        var asignacionPrevia = await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        asignacionPrevia.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que la asignacion previa al reingreso funcione");

        await TerminarVinculacionAsync(
            ComputarStreamId(numeroIdentificacion), codigo, new DateOnly(2026, 6, 1), ct);
        await IniciarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada);

        asignaciones.Should().Be(2,
            "la vinculacion nueva del reingreso deberia aceptar la asignacion como una categoria nueva, agregando otro evento sin colisionar con la etiqueta congelada de la vinculacion anterior");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;
        var idSinGuion = NuevoNumeroIdentificacion();

        var response = await AsignarEtiquetaAsync(idSinGuion, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoTipoDeLaIdentificacionEnLaRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = $"XX-{NuevoNumeroIdentificacion()}";

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoNumeroDeLaIdentificacionEnLaRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        const string idConNumeroVacio = "CC-";

        var response = await AsignarEtiquetaAsync(idConNumeroVacio, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoValorDelBodyEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = ComputarStreamId(NuevoNumeroIdentificacion());

        var response = await AsignarEtiquetaAsync(id, "Área", valor: "", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
