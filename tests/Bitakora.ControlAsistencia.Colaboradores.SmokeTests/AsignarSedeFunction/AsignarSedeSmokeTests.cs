// Sin ServiceBusFixture: el comando es event-sourcing puro, sin consumidores downstream -- la unica
// verificacion black-box de sus efectos es leer mt_events via PostgresFixture.
// El arrange registra el colaborador (y termina o reinicia su vinculacion cuando aplica) via los
// mismos comandos HTTP que originan esos hechos, nunca sembrando el event store por fuera del API.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.AsignarSedeFunction;

public class AsignarSedeSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoSedeAsignada = "sede_asignada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Segunda lectura de un evento que ExisteEventoAsync ya espero: no hay nada mas que esperar.
    private static readonly TimeSpan TimeoutLecturaConfirmada = TimeSpan.FromSeconds(5);

    // Numero unico por test: la identidad del stream es la identificacion, no un Guid por llamada,
    // asi que sin esto dos corridas colisionarian sobre el mismo stream.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente (MEF-ADR-0002): se recompone a mano, no se deriva de
    // Identificacion.ToString(), para que un cambio de formato del VO no se auto-valide.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static string RutaSede(string id) => $"/api/colaboradores/{id}/sede";

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

    private static object PayloadCodigoSede(string codigoSede) => new { codigoSede };

    // Devuelve el codigo de la vinculacion inicial, que el arrange usa como {codigo} de ruta al
    // terminarla.
    private async Task<string> RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var codigo = NuevoCodigoColaborador();

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio, codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
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

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    // Reingreso: vinculacion nueva sobre un colaborador cuya vinculacion anterior ya termino.
    private async Task IniciarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{ComputarStreamId(numeroIdentificacion)}/vinculaciones",
            PayloadIniciarVinculacion(NuevoCodigoColaborador(), fechaInicio),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que IniciarVinculacion funcione");
    }

    private Task<HttpResponseMessage> AsignarSedeAsync(string id, string codigoSede, CancellationToken ct) =>
        _client.PutAsJsonAsync(RutaSede(id), PayloadCodigoSede(codigoSede), ct);

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
    public async Task AsignarSede_Retorna202YPersisteSedeAsignada_CuandoColaboradorNoTieneSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, id, TipoEventoSedeAsignada, Timeout);

        eventoPersistido.GetProperty("CodigoSede").GetString().Should().Be("BOG");
    }

    // Reemplazo puro: el conteo pasa de 1 a 2, sin evento de retiro intermedio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna202YAgregaOtroEvento_CuandoSedeEsDistintaALaVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 20), ct);

        var primeraAsignacion = await AsignarSedeAsync(id, "BOG", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraSede = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada, Timeout);
        existePrimeraSede.Should().BeTrue(
            $"el evento {TipoEventoSedeAsignada} de la primera asignacion deberia estar en el stream {id}");

        var segundaAsignacion = await AsignarSedeAsync(id, "MED", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada);

        asignaciones.Should().Be(2,
            "reasignar una sede distinta a la vigente deberia agregar un evento nuevo (reemplazo puro, sin retiro)");

        var eventoConSedeNueva = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, id, TipoEventoSedeAsignada, "CodigoSede", "MED", TimeoutLecturaConfirmada);

        eventoConSedeNueva.GetProperty("CodigoSede").GetString().Should().Be("MED");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna202SinNuevoEvento_CuandoSedeEsIdenticaALaVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 25), ct);

        var primeraAsignacion = await AsignarSedeAsync(id, "BOG", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraSede = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada, Timeout);
        existePrimeraSede.Should().BeTrue(
            $"el evento {TipoEventoSedeAsignada} de la primera asignacion deberia estar en el stream {id}");

        var segundaAsignacion = await AsignarSedeAsync(id, "BOG", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada);

        asignaciones.Should().Be(1,
            "asignar la misma sede ya vigente no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna409_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);
        await TerminarVinculacionAsync(
            ComputarStreamId(numeroIdentificacion), codigo, new DateOnly(2026, 5, 1), ct);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // La terminacion bloquea aunque su fecha efectiva no haya llegado: no se consulta el reloj.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna409_CuandoTerminacionEsUnPreavisoConFechaFutura()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);
        var fechaPreavisoFutura = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(ComputarStreamId(numeroIdentificacion), codigo, fechaPreavisoFutura, ct);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Prueba indirecta de que el reingreso deja al colaborador sin sede: sin esa limpieza, asignar
    // el mismo codigo de antes seria idempotencia y el conteo se quedaria en 1. Es la unica lectura
    // black-box disponible mientras el dominio no exponga una vista de la sede vigente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna202YAgregaOtroEvento_CuandoVinculacionEsUnReingresoConLaMismaSedeQueTeniaAntesDeTerminar()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 10), ct);

        var asignacionPrevia = await AsignarSedeAsync(id, "BOG", ct);
        asignacionPrevia.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la asignacion previa al reingreso funcione");

        await TerminarVinculacionAsync(
            ComputarStreamId(numeroIdentificacion), codigo, new DateOnly(2026, 6, 1), ct);
        await IniciarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoSedeAsignada);

        asignaciones.Should().Be(2,
            "el reingreso deberia dejar al colaborador sin sede: asignar el mismo codigo de antes de terminar no deberia tratarse como SinCambios");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado
        var id = ComputarStreamId(numeroIdentificacion);

        var response = await AsignarSedeAsync(id, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna400_CuandoCodigoSedeDelBodyEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = ComputarStreamId(NuevoNumeroIdentificacion());

        var response = await AsignarSedeAsync(id, codigoSede: "", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Sanity de que este endpoint invoca la guarda compartida IdentificacionDeRuta, ya cubierta
    // exhaustivamente por AsignarEtiquetaSmokeTests.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSede_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;
        var idSinGuion = NuevoNumeroIdentificacion(); // p.ej. "3F2A0C..." sin "CC-" adelante

        var response = await AsignarSedeAsync(idSinGuion, "BOG", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
