using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.RetirarCentroDeCostosFunction;

// CentroDeCostosRetirado no cruza el bus en este issue: la unica verificacion black-box de los
// efectos del handler es leer mt_events via PostgresFixture -- no hay ServiceBusFixture que
// consultar.
public class RetirarCentroDeCostosSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoCentroDeCostosAsignado = "centro_de_costos_asignado";
    private const string TipoEventoCentroDeCostosRetirado = "centro_de_costos_retirado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que la forma local de este archivo es PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Forma local deliberadamente desacoplada de ReadModels.Sedes.FichaSede: replica el shape JSON
    // de GET sedes/fichas/{codigo} como oraculo independiente -- no se referencia el tipo real.
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
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Recomputo local del streamId: oraculo independiente, sin referenciar ComputarStreamId.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static string RutaCentroDeCostos(string codigo) => $"/api/sedes/{codigo}/centro-de-costos";

    private static string RutaFicha(string codigo) => $"/api/sedes/fichas/{codigo}";

    // Reintenta el GET hasta que la proyeccion asincrona materialice la ficha: el 404 transitorio
    // es el worker que todavia no la aplico (MEF-ADR-0034), no una sede inexistente.
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
        var codigo = NuevoCodigo();
        var payload = new { codigo, nombre = "[TEST] Sede Original", ciudad = (string?)null, direccion = (string?)null };

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que el registro previo funcione");

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeRegistrada, Timeout);
        existe.Should().BeTrue(
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId} antes de retirar el centro de costos");

        return codigo;
    }

    private async Task<string> RegistrarSedeConCentroDeCostosAsync(CancellationToken ct)
    {
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);

        var asignacion = await _client.PutAsJsonAsync(
            RutaCentroDeCostos(codigo), new { centroDeCostos = "CC-VIGENTE" }, ct);
        asignacion.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que la asignacion previa funcione");

        var existeAsignacion = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosAsignado, Timeout,
            campoJson: "CentroDeCostos", valorJson: "CC-VIGENTE");
        existeAsignacion.Should().BeTrue(
            $"el evento {TipoEventoCentroDeCostosAsignado} deberia existir en el stream {streamId} antes de retirarlo");

        return codigo;
    }

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
    public async Task RetirarCentroDeCostos_Retorna204YPersisteCentroDeCostosRetirado_CuandoHayCentroDeCostosVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeConCentroDeCostosAsync(ct);

        var response = await _client.DeleteAsync(RutaCentroDeCostos(codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosRetirado, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoCentroDeCostosRetirado} deberia existir en el stream {streamId}");
    }

    // Estado ya alcanzado (MEF-ADR-0004): sin CC vigente el DELETE es un no-op exitoso -- 204 sin
    // evento y sin alterar la ficha de la sede.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarCentroDeCostos_Retorna204SinEvento_CuandoNoHayCentroDeCostosVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);
        var fichaPrevia = await EsperarFichaAsync(codigo, ct);

        var response = await _client.DeleteAsync(RutaCentroDeCostos(codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosRetirado);
        registros.Should().Be(0,
            "el estado ya alcanzado no debe persistir un evento de retiro (MEF-ADR-0004)");

        var fichaTrasNoOp = await _client.GetAsync(RutaFicha(codigo), ct);
        fichaTrasNoOp.StatusCode.Should().Be(HttpStatusCode.OK);
        var contenido = await fichaTrasNoOp.Content.ReadFromJsonAsync<FichaSedeRespuestaSmoke>(
            JsonOptions, ct);
        contenido.Should().BeEquivalentTo(fichaPrevia,
            "el no-op no debe alterar la ficha de la sede");
    }

    // Secuencia canonica del no-op de un DELETE (MEF-ADR-0004, MEF-ADR-0043 punto 10):
    // asignar -> retirar -> retirar de nuevo. El segundo DELETE repite verbo, ruta e identidad y
    // vuelve a responder 204 sin agregar un evento nuevo al stream (conteo estable en 1).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarCentroDeCostos_Retorna204SinEventoNuevo_CuandoSeRetiraDosVeces()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeConCentroDeCostosAsync(ct);
        var streamId = ComputarStreamId(codigo);

        var primerRetiro = await _client.DeleteAsync(RutaCentroDeCostos(codigo), ct);
        primerRetiro.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var persistio = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosRetirado, Timeout);
        persistio.Should().BeTrue("el primer retiro si es un cambio y debe persistir su evento");

        var segundoRetiro = await _client.DeleteAsync(RutaCentroDeCostos(codigo), ct);

        segundoRetiro.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await segundoRetiro.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosRetirado);
        registros.Should().Be(1,
            "el segundo retiro es estado ya alcanzado: no agrega un evento nuevo (MEF-ADR-0004)");
    }

    // El charset URL-safe del codigo tambien rige cuando viaja en la ruta: "!" queda fuera del set
    // unreserved y se rechaza con 400, nunca con el 404 de un stream inexistente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarCentroDeCostos_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync(
            RutaCentroDeCostos($"TEST!{Guid.CreateVersion7()}"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarCentroDeCostos_Retorna404_CuandoSedeNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync(RutaCentroDeCostos(NuevoCodigo()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
