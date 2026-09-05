using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AsignarSedeAFranjaFunction;

public class AsignarSedeAFranjaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaTurnos = "/api/programacion/turnos";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoSedeAsignada = "sede_de_franja_asignada";
    private const string TipoEventoSedeRetirada = "sede_de_franja_retirada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string RutaAsignarSedeAFranja(Guid turnoId) =>
        $"{RutaTurnos}/{turnoId}:asignar-sede-franja";

    private static string RutaAgregarFranja(Guid turnoId) => $"{RutaTurnos}/{turnoId}:agregar-franja";

    private async Task CrearTurnoVacioAsync(Guid turnoId, string nombreBase, CancellationToken ct)
    {
        var payload = new { turnoId, nombre = $"{nombreBase} {turnoId}" };
        var response = await _client.PostAsJsonAsync(RutaTurnos, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    private async Task AgregarFranjaSinSedeAsync(Guid turnoId, CancellationToken ct)
    {
        var payload = new { inicio = "14:00:00", fin = "22:00:00" };
        var response = await _client.PostAsJsonAsync(RutaAgregarFranja(turnoId), payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AgregarFranja funcione");
    }

    // CA-6: cuarto paso del diseno de turno por pasos -- crear el turno, agregarle una franja sin
    // sede, asignarle una sede prearmada (202 + sede_de_franja_asignada en mt_events), retirarla
    // (202 + sede_de_franja_retirada sin la clave "sede") y un tercer retiro -> 409 (nada que
    // retirar, FranjaSinSede).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeAFranja_DebeRetornar202YPersistirLaSede_CuandoLaFranjaNoTeniaSede()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Asignar Sede", ct);
        await AgregarFranjaSinSedeAsync(turnoId, ct);

        var payloadAsignar = new
        {
            franja = "14:00",
            sede = new { id = "SEDE-CHAPINERO", nombre = "[TEST] Chapinero" }
        };
        var respuestaAsignar = await _client.PostAsJsonAsync(
            RutaAsignarSedeAFranja(turnoId), payloadAsignar, ct);

        respuestaAsignar.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = turnoId.ToString();
        var eventoAsignado = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoSedeAsignada,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        eventoAsignado.GetProperty("Franja").GetProperty("sede").GetProperty("id").GetString()
            .Should().Be("SEDE-CHAPINERO");

        var payloadRetirar = new { franja = "14:00" };
        var respuestaRetirar = await _client.PostAsJsonAsync(
            RutaAsignarSedeAFranja(turnoId), payloadRetirar, ct);

        respuestaRetirar.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var eventoRetirado = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoSedeRetirada,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        eventoRetirado.GetProperty("Franja").TryGetProperty("sede", out _).Should().BeFalse();

        var terceraRespuesta = await _client.PostAsJsonAsync(
            RutaAsignarSedeAFranja(turnoId), payloadRetirar, ct);

        terceraRespuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeAFranja_DebeRetornar404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { franja = "14:00" };

        var response = await _client.PostAsJsonAsync(
            RutaAsignarSedeAFranja(Guid.CreateVersion7()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-3: sobre un turno sin franjas, cualquier hora cae en FranjaNoExiste -> 409.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeAFranja_DebeRetornar409_CuandoLaFranjaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Sin Franjas Para Sede", ct);

        var payload = new { franja = "14:00" };
        var response = await _client.PostAsJsonAsync(RutaAsignarSedeAFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // El {id} de ruta se valida en el borde (MEF-ADR-0037 seccion 2): un id no-Guid nunca llega
    // al command router.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeAFranja_DebeRetornar400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { franja = "14:00" };

        var response = await _client.PostAsJsonAsync(
            $"{RutaTurnos}/no-es-un-guid:asignar-sede-franja", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Invariante del VO (FranjaOrdinaria.Crear via ConSede): una sede con Id en blanco es
    // SedeIncompleta -- el handler deja subir la ArgumentException sin envolverla (CA-ADR-0030),
    // el endpoint la traduce a 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeAFranja_DebeRetornar400_CuandoLaSedeEsIncompleta()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        await CrearTurnoVacioAsync(turnoId, "[TEST] Turno Sede Incompleta", ct);
        await AgregarFranjaSinSedeAsync(turnoId, ct);

        var payload = new { franja = "14:00", sede = new { id = "", nombre = "[TEST] Sin Id" } };
        var response = await _client.PostAsJsonAsync(RutaAsignarSedeAFranja(turnoId), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
