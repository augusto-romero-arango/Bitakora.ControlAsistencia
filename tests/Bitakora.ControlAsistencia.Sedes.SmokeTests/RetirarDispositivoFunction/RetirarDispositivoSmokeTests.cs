// DispositivoRetirado no cruza el bus: la unica verificacion black-box de los efectos del handler
// es leer mt_events via PostgresFixture -- de ahi la ausencia de ServiceBusFixture.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.RetirarDispositivoFunction;

public class RetirarDispositivoSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrarSede = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoDispositivoInstalado = "dispositivo_instalado";
    private const string TipoEventoDispositivoRetirado = "dispositivo_retirado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que la forma local de este archivo es PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Forma local DESACOPLADA del read model de produccion (ReadModels.Sedes.FichaSede): replica
    // solo el shape JSON de GET sedes/fichas/{codigo}, usado aqui como oraculo de "la ficha queda
    // identica" -- no como sujeto de este archivo (issue #664 no toca ObtenerFichaSede).
    private sealed record FichaSedeRespuestaSmoke(
        string Id,
        string Codigo,
        string Nombre,
        string? Ciudad,
        string? Direccion,
        string? CentroDeCostos,
        bool Activa,
        IReadOnlyList<string> Dispositivos);

    // Prefijo "TEST-" y no "[TEST] ": el Codigo viaja en la ruta y esta sujeto al charset URL-safe,
    // del que "[", "]" y el espacio quedan fuera.
    private static string NuevoCodigoSede() => $"TEST-{Guid.CreateVersion7()}";

    private static string NuevoDispositivoId() => $"TEST-DISPOSITIVO-{Guid.CreateVersion7()}";

    // Recomputo local del streamId: oraculo independiente, sin referenciar ComputarStreamId.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static string RutaDispositivos(string codigo) => $"/api/sedes/{codigo}/dispositivos";

    private static string RutaDispositivo(string codigo, string dispositivoId) =>
        $"/api/sedes/{codigo}/dispositivos/{dispositivoId}";

    private static string RutaFicha(string codigo) => $"/api/sedes/fichas/{codigo}";

    // Reintenta el GET hasta que la proyeccion asincrona materialice la ficha (404 = el worker
    // todavia no la aplico). Solo se usa para la primera lectura de cada test; la segunda lectura
    // (tras el no-op) ya sabe que la ficha existe y consulta directo.
    private Task<FichaSedeRespuestaSmoke> EsperarFichaAsync(string codigo, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(RutaFicha(codigo), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadFromJsonAsync<FichaSedeRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);
        }, Timeout);

    private async Task<string> RegistrarSedeDePruebaAsync(CancellationToken ct)
    {
        var codigo = NuevoCodigoSede();
        var payload = new { codigo, nombre = "[TEST] Sede Original", ciudad = (string?)null, direccion = (string?)null };

        var response = await _client.PostAsJsonAsync(RutaRegistrarSede, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que el registro previo de la sede funcione");

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeRegistrada, Timeout);
        existe.Should().BeTrue(
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId} antes de retirar el dispositivo");

        return codigo;
    }

    private async Task<(string Codigo, string DispositivoId)> RegistrarSedeConDispositivoInstaladoAsync(
        CancellationToken ct)
    {
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);
        var dispositivoId = NuevoDispositivoId();

        var instalacion = await _client.PostAsJsonAsync(
            RutaDispositivos(codigo), new { dispositivoId }, ct);
        instalacion.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que la instalacion previa funcione");

        var existeInstalacion = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoDispositivoInstalado, Timeout,
            campoJson: "DispositivoId", valorJson: dispositivoId);
        existeInstalacion.Should().BeTrue(
            $"el evento {TipoEventoDispositivoInstalado} deberia existir en el stream {streamId} antes de retirarlo");

        return (codigo, dispositivoId);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-3
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarDispositivo_Retorna204YPersisteDispositivoRetirado_CuandoDispositivoEstaInstalado()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var (codigo, dispositivoId) = await RegistrarSedeConDispositivoInstaladoAsync(ct);

        var response = await _client.DeleteAsync(RutaDispositivo(codigo, dispositivoId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoDispositivoRetirado, Timeout,
            campoJson: "DispositivoId", valorJson: dispositivoId);

        existe.Should().BeTrue(
            $"el evento {TipoEventoDispositivoRetirado} deberia existir en el stream {streamId}");
    }

    // CA-4/#664: estado ya alcanzado (MEF-ADR-0004) -- un dispositivoId no instalado en esta sede
    // (nunca instalado o ya retirado) es un no-op exitoso, sin evento y sin alterar la ficha de la
    // sede (CA-ADR-0030). Decision del experto (2026-09-12): mismo 204 sin distinguir "nunca
    // existio" de "ya fue retirado" -- ver notas del issue.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarDispositivo_Retorna204SinEvento_CuandoDispositivoNoInstaladoEnEstaSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);
        var dispositivoId = NuevoDispositivoId();
        var fichaPrevia = await EsperarFichaAsync(codigo, ct);

        var response = await _client.DeleteAsync(RutaDispositivo(codigo, dispositivoId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoDispositivoRetirado);
        registros.Should().Be(0,
            "el estado ya alcanzado no debe persistir un evento de retiro (MEF-ADR-0004)");

        var fichaTrasNoOp = await _client.GetAsync(RutaFicha(codigo), ct);
        fichaTrasNoOp.StatusCode.Should().Be(HttpStatusCode.OK);
        var contenido = await fichaTrasNoOp.Content.ReadFromJsonAsync<FichaSedeRespuestaSmoke>(
            JsonOptions, ct);
        contenido.Should().BeEquivalentTo(fichaPrevia,
            "el no-op no debe alterar la ficha de la sede");
    }

    // El charset URL-safe del codigo tambien rige cuando viaja en la ruta: "!" queda fuera del set
    // unreserved y se rechaza con 400, nunca con el 404 de un stream inexistente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarDispositivo_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync(
            RutaDispositivo($"TEST!{Guid.CreateVersion7()}", NuevoDispositivoId()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarDispositivo_Retorna404_CuandoSedeNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync(
            RutaDispositivo(NuevoCodigoSede(), NuevoDispositivoId()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
