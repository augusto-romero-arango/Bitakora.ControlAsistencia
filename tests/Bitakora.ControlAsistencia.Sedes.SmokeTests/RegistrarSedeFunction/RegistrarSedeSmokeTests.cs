// Issue #456: smoke tests del endpoint POST sedes (primer comando del ciclo de vida de
// SedeAggregateRoot). Comando event-sourcing puro sin consumidores downstream (CA-ADR-0030):
// SedeRegistrada no cruza el bus en este issue, asi que no hay ServiceBusFixture -- la unica
// verificacion black-box de los efectos del handler es leer mt_events via PostgresFixture.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.RegistrarSedeFunction;

public class RegistrarSedeSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Prefijo "TEST-" en vez de "[TEST] ": Codigo esta sujeto al charset URL-safe (CA-4) y "[", "]"
    // y el espacio quedan fuera de ese set -- el prefijo de datos de prueba va en Nombre, que no
    // tiene esa restriccion.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Recomputo local del streamId (oraculo independiente, MEF-ADR-0002): no se referencia
    // SedeAggregateRoot.ComputarStreamId desde el smoke test.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static object PayloadRegistro(
        string codigo, string nombre = "[TEST] Sede Smoke",
        string? ciudad = null, string? direccion = null) => new
        {
            codigo,
            nombre,
            ciudad,
            direccion
        };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: codigo y nombre validos, con ciudad y direccion -> 202 y SedeRegistrada persistido con
    // los cuatro campos.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_Retorna202YPersisteSedeRegistrada_CuandoCodigoYNombreSonValidos()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = NuevoCodigo();
        var payload = PayloadRegistro(
            codigo, "[TEST] Sede Norte", ciudad: "Bogota", direccion: "Calle 1 # 2-3");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeRegistrada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId}");

        var evento = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaSedes, streamId, TipoEventoSedeRegistrada, TimeSpan.FromSeconds(5));

        evento.GetProperty("Codigo").GetString().Should().Be(codigo);
        evento.GetProperty("Nombre").GetString().Should().Be("[TEST] Sede Norte");
        evento.GetProperty("Ciudad").GetString().Should().Be("Bogota");
        evento.GetProperty("Direccion").GetString().Should().Be("Calle 1 # 2-3");
    }

    // CA-2: Ciudad y Direccion son opcionales -- el registro sin ellas persiste el evento con esos
    // campos nulos.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_Retorna202YPersisteCiudadYDireccionNulos_CuandoNoLlegan()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = NuevoCodigo();
        var payload = PayloadRegistro(codigo, "[TEST] Sede Sin Ubicacion");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeRegistrada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId}");

        var evento = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaSedes, streamId, TipoEventoSedeRegistrada, TimeSpan.FromSeconds(5));

        evento.GetProperty("Ciudad").ValueKind.Should().Be(JsonValueKind.Null);
        evento.GetProperty("Direccion").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // CA-5: codigo ya registrado -> 409, sin un segundo sede_registrada en el stream (resultado
    // declinado, CA-ADR-0030 -- ningun evento de fallo persistido).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_Retorna409YNoDuplicaEvento_CuandoCodigoYaExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = NuevoCodigo();

        var primerRegistro = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(codigo, "[TEST] Sede Original"), ct);
        primerRegistro.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que el primer registro funcione");

        var streamId = ComputarStreamId(codigo);
        var existePrimerRegistro = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeRegistrada, Timeout);
        existePrimerRegistro.Should().BeTrue(
            $"el evento {TipoEventoSedeRegistrada} del primer registro deberia estar en el stream {streamId} antes de reintentar");

        var segundoRegistro = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(codigo, "[TEST] Sede Duplicada"), ct);

        segundoRegistro.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoSedeRegistrada);

        registros.Should().Be(1,
            "el segundo registro se rechazo con 409: no debe haber escrito un segundo sede_registrada");
    }

    // CA-3: Codigo vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_Retorna400_CuandoCodigoEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro("", "[TEST] Sede Sin Codigo");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: Nombre vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_Retorna400_CuandoNombreEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro(NuevoCodigo(), "");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-4: ":" esta explicitamente fuera del set permitido -- es el separador de la anatomia del
    // stream (CA-ADR-0031, "s:{codigo}"). Un codigo con ":" rompe el split de esa anatomia.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_Retorna400_CuandoCodigoContieneDosPuntos()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro($"TEST:{Guid.CreateVersion7()}", "[TEST] Sede Codigo Invalido");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-4: cualquier otro caracter fuera del set unreserved (espacio, aqui) -> 400, nunca
    // normalizacion silenciosa (MEF-ADR-0043 seccion 1.2).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_Retorna400_CuandoCodigoContieneEspacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRegistro($"TEST {Guid.CreateVersion7()}", "[TEST] Sede Codigo Invalido");

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
