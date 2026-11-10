// DispositivoInstalado no cruza el bus: sus efectos se verifican leyendo mt_events.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.InstalarDispositivoFunction;

public class InstalarDispositivoSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrarSede = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoDispositivoInstalado = "dispositivo_instalado";
    private const string TipoEventoDispositivoRetirado = "dispositivo_retirado";
    private const string TablaUbicacionDispositivo = "mt_doc_ubicaciondispositivo";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // UbicacionDispositivo materializa via el daemon Async (MEF-ADR-0034 seccion 3): ventana propia,
    // mas holgada que Timeout, para no acoplar la espera del daemon a la de los efectos inline.
    private static readonly TimeSpan TimeoutProyeccion = TimeSpan.FromSeconds(120);

    // "TEST-" y no "[TEST] ": el Codigo viaja en la ruta y "[", "]" y el espacio no son URL-safe.
    private static string NuevoCodigoSede() => $"TEST-{Guid.CreateVersion7()}";

    private static string NuevoDispositivoId() => $"TEST-DISPOSITIVO-{Guid.CreateVersion7()}";

    // Recomputo local del streamId: oraculo independiente, sin referenciar ComputarStreamId.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static string RutaDispositivos(string codigo) => $"/api/sedes/{codigo}/dispositivos";

    private static string RutaDispositivo(string codigo, string dispositivoId) =>
        $"/api/sedes/{codigo}/dispositivos/{dispositivoId}";

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
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId} antes de instalar el dispositivo");

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
    public async Task InstalarDispositivo_Retorna201YPersisteDispositivoInstalado_CuandoDispositivoIdEsValido()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var dispositivoId = NuevoDispositivoId();

        var response = await _client.PostAsJsonAsync(
            RutaDispositivos(codigo), new { dispositivoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be(new Uri($"/api/sedes/fichas/{codigo}", UriKind.Relative));

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoDispositivoInstalado, Timeout,
            campoJson: "DispositivoId", valorJson: dispositivoId);

        existe.Should().BeTrue(
            $"el evento {TipoEventoDispositivoInstalado} deberia existir en el stream {streamId}");

        var evento = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaSedes, streamId, TipoEventoDispositivoInstalado,
            "DispositivoId", dispositivoId, TimeSpan.FromSeconds(5));

        evento.GetProperty("DispositivoId").GetString().Should().Be(dispositivoId);
    }

    // Declina sin persistir un segundo evento (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task InstalarDispositivo_Retorna409YNoDuplicaEvento_CuandoDispositivoYaInstaladoEnEstaSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);
        var dispositivoId = NuevoDispositivoId();

        var primeraInstalacion = await _client.PostAsJsonAsync(
            RutaDispositivos(codigo), new { dispositivoId }, ct);
        primeraInstalacion.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que la primera instalacion funcione");

        var existePrimeraInstalacion = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoDispositivoInstalado, Timeout,
            campoJson: "DispositivoId", valorJson: dispositivoId);
        existePrimeraInstalacion.Should().BeTrue(
            $"el evento {TipoEventoDispositivoInstalado} deberia estar en el stream {streamId} antes de reintentar");

        var segundaInstalacion = await _client.PostAsJsonAsync(
            RutaDispositivos(codigo), new { dispositivoId }, ct);

        segundaInstalacion.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoDispositivoInstalado);
        registros.Should().Be(1,
            "el segundo intento se rechazo con 409: no debe haber escrito un segundo dispositivo_instalado");
    }

    // UbicacionDispositivo materializa de forma asincrona: se espera el documento
    // mt_doc_ubicaciondispositivo en Postgres (no hay Function GET sobre esta vista) y solo despues
    // se hace el POST al destino, UNA sola vez. Reintentar el POST mientras la vista no materializa
    // instalaria y retiraria un dispositivo fantasma en cada iteracion, con eventos reales.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task InstalarDispositivo_Retorna409YNoInstalaEnDestino_CuandoDispositivoYaEstaInstaladoEnOtraSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoOrigen = await RegistrarSedeDePruebaAsync(ct);
        var codigoDestino = await RegistrarSedeDePruebaAsync(ct);
        var streamOrigen = ComputarStreamId(codigoOrigen);
        var streamDestino = ComputarStreamId(codigoDestino);
        var dispositivoId = NuevoDispositivoId();

        var instalacionOrigen = await _client.PostAsJsonAsync(
            RutaDispositivos(codigoOrigen), new { dispositivoId }, ct);
        instalacionOrigen.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que la instalacion en la sede origen funcione");

        var existeEnOrigen = await postgres.ExisteEventoAsync(
            SchemaSedes, streamOrigen, TipoEventoDispositivoInstalado, Timeout,
            campoJson: "DispositivoId", valorJson: dispositivoId);
        existeEnOrigen.Should().BeTrue(
            $"el evento {TipoEventoDispositivoInstalado} deberia estar en el stream {streamOrigen} antes de intentar el cruce");

        var proyeccionMaterializada = await postgres.ExisteDocumentoAsync(
            SchemaSedes, TablaUbicacionDispositivo, dispositivoId, TimeoutProyeccion,
            campoJson: "SedeId", valorJson: streamOrigen);
        proyeccionMaterializada.Should().BeTrue(
            $"UbicacionDispositivo deberia materializar la sede origen {streamOrigen} para {dispositivoId} " +
            $"(tabla {SchemaSedes}.{TablaUbicacionDispositivo}, tenant {postgres.TenantId}) antes de ejercitar el cruce");

        var intento = await _client.PostAsJsonAsync(
            RutaDispositivos(codigoDestino), new { dispositivoId }, ct);

        intento.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var registrosEnDestino = await postgres.ContarEventosAsync(
            SchemaSedes, streamDestino, TipoEventoDispositivoInstalado);
        registrosEnDestino.Should().Be(0,
            "el rechazo cross-sede ocurre antes de cargar el aggregate destino: el stream de la sede destino no debe recibir ningun dispositivo_instalado");
    }

    // No viola la exclusividad: el dispositivo retirado ya no esta instalado en esta sede.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task InstalarDispositivo_Retorna201YPersisteSegundoEvento_CuandoReinstalaDispositivoPreviamenteRetirado()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);
        var dispositivoId = NuevoDispositivoId();

        var instalacion = await _client.PostAsJsonAsync(
            RutaDispositivos(codigo), new { dispositivoId }, ct);
        instalacion.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que la instalacion inicial funcione");

        var retiro = await _client.DeleteAsync(RutaDispositivo(codigo, dispositivoId), ct);
        retiro.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que el retiro previo funcione");

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoDispositivoRetirado, Timeout,
            campoJson: "DispositivoId", valorJson: dispositivoId);
        existeRetiro.Should().BeTrue(
            $"el evento {TipoEventoDispositivoRetirado} deberia estar en el stream {streamId} antes de reinstalar");

        var reinstalacion = await _client.PostAsJsonAsync(
            RutaDispositivos(codigo), new { dispositivoId }, ct);

        reinstalacion.StatusCode.Should().Be(HttpStatusCode.Created);

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoDispositivoInstalado);
        registros.Should().Be(2,
            "instalar, retirar y reinstalar el mismo dispositivo agrega un segundo dispositivo_instalado");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task InstalarDispositivo_Retorna400_CuandoDispositivoIdEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { dispositivoId = "" };

        var response = await _client.PostAsJsonAsync(
            RutaDispositivos(NuevoCodigoSede()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // El DispositivoId viaja luego como segmento de ruta en el DELETE (MEF-ADR-0043 seccion 1.3).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task InstalarDispositivo_Retorna400_CuandoDispositivoIdNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { dispositivoId = "TEST DISPOSITIVO!" };

        var response = await _client.PostAsJsonAsync(
            RutaDispositivos(NuevoCodigoSede()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // "!" no es unreserved: 400, nunca el 404 de un stream inexistente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task InstalarDispositivo_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { dispositivoId = NuevoDispositivoId() };

        var response = await _client.PostAsJsonAsync(
            RutaDispositivos($"TEST!{Guid.CreateVersion7()}"), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task InstalarDispositivo_Retorna404_CuandoSedeNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { dispositivoId = NuevoDispositivoId() };

        var response = await _client.PostAsJsonAsync(
            RutaDispositivos(NuevoCodigoSede()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
