// DispositivoInstalado no cruza el bus: la unica verificacion black-box de los efectos del handler
// es leer mt_events via PostgresFixture -- de ahi la ausencia de ServiceBusFixture.
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
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Prefijo "TEST-" y no "[TEST] ": el Codigo viaja en la ruta y esta sujeto al charset URL-safe,
    // del que "[", "]" y el espacio quedan fuera.
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
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

    // CA-1
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task InstalarDispositivo_Retorna202YPersisteDispositivoInstalado_CuandoDispositivoIdEsValido()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var dispositivoId = NuevoDispositivoId();

        var response = await _client.PostAsJsonAsync(
            RutaDispositivos(codigo), new { dispositivoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

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

    // CA-2: declina sin persistir un segundo evento (CA-ADR-0030).
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
        primeraInstalacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
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

    // Issue #477 CA-1: rechazo cross-sede antes de cargar el aggregate destino. UbicacionDispositivo
    // materializa de forma ASINCRONA (MEF-ADR-0034 seccion 3) y no tiene endpoint GET propio, asi
    // que no hay forma black-box de esperar su materializacion sin reintentar el propio comando.
    // Reintentar el POST a destino tiene un efecto real: mientras la vista no haya materializado
    // SedeId=origen, el intento en destino puede colarse (202) -- la ventana best-effort que el
    // propio issue documenta. Se limpia esa instalacion fantasma retirandola (la remediacion que
    // el issue senala explicitamente) y se reintenta hasta que la vista alcance al event store; la
    // asercion de "sin evento nuevo" compara el conteo justo antes/despues del intento que SI
    // resuelve en 409, no el historico completo (que puede incluir fantasmas ya retirados).
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
        instalacionOrigen.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la instalacion en la sede origen funcione");

        var existeEnOrigen = await postgres.ExisteEventoAsync(
            SchemaSedes, streamOrigen, TipoEventoDispositivoInstalado, Timeout,
            campoJson: "DispositivoId", valorJson: dispositivoId);
        existeEnOrigen.Should().BeTrue(
            $"el evento {TipoEventoDispositivoInstalado} deberia estar en el stream {streamOrigen} antes de intentar el cruce");

        HttpStatusCode? ultimoStatus = null;

        var rechazado = await Polling.WaitUntilTrueAsync(async () =>
        {
            var eventosAntes = await postgres.ContarEventosAsync(
                SchemaSedes, streamDestino, TipoEventoDispositivoInstalado);

            var intento = await _client.PostAsJsonAsync(
                RutaDispositivos(codigoDestino), new { dispositivoId }, ct);
            ultimoStatus = intento.StatusCode;

            if (intento.StatusCode == HttpStatusCode.Accepted)
            {
                await _client.DeleteAsync(RutaDispositivo(codigoDestino, dispositivoId), ct);
                return false;
            }

            if (intento.StatusCode != HttpStatusCode.Conflict)
                throw new InvalidOperationException(
                    $"Respuesta inesperada al instalar en destino: {intento.StatusCode}");

            var eventosDespues = await postgres.ContarEventosAsync(
                SchemaSedes, streamDestino, TipoEventoDispositivoInstalado);
            eventosDespues.Should().Be(eventosAntes,
                "el rechazo cross-sede debe ocurrir antes de cargar el aggregate destino: no debe agregar un nuevo dispositivo_instalado");

            return true;
        }, Timeout);

        rechazado.Should().BeTrue(
            "la instalacion cross-sede deberia terminar rechazada con 409 una vez que UbicacionDispositivo materialice la sede origen");
        ultimoStatus.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-6: reinstalar un dispositivo previamente retirado de esta sede procede -- no es la misma
    // invariante de exclusividad que CA-2 (ese dispositivo ya no esta instalado en esta sede).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task InstalarDispositivo_Retorna202YPersisteSegundoEvento_CuandoReinstalaDispositivoPreviamenteRetirado()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);
        var dispositivoId = NuevoDispositivoId();

        var instalacion = await _client.PostAsJsonAsync(
            RutaDispositivos(codigo), new { dispositivoId }, ct);
        instalacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la instalacion inicial funcione");

        var retiro = await _client.DeleteAsync(RutaDispositivo(codigo, dispositivoId), ct);
        retiro.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que el retiro previo funcione");

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoDispositivoRetirado, Timeout,
            campoJson: "DispositivoId", valorJson: dispositivoId);
        existeRetiro.Should().BeTrue(
            $"el evento {TipoEventoDispositivoRetirado} deberia estar en el stream {streamId} antes de reinstalar");

        var reinstalacion = await _client.PostAsJsonAsync(
            RutaDispositivos(codigo), new { dispositivoId }, ct);

        reinstalacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoDispositivoInstalado);
        registros.Should().Be(2,
            "instalar, retirar y reinstalar el mismo dispositivo agrega un segundo dispositivo_instalado");
    }

    // CA-5
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

    // CA-5: mismo charset URL-safe de CodigoSede -- el DispositivoId se expone luego como segmento
    // de ruta en el DELETE (MEF-ADR-0043 seccion 1.3).
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

    // El charset URL-safe del codigo tambien rige cuando viaja en la ruta: "!" queda fuera del set
    // unreserved y se rechaza con 400, nunca con el 404 de un stream inexistente.
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
