// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa CuadroDeTurnos despues de
// persistir los eventos, por eso el caso de exito reintenta el GET con Polling.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ObtenerCuadroDeTurnos;

public class ObtenerCuadroDeTurnosSmokeTests(ApiFixture api) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private HttpClient _client => _tenant.Client;

    private const string RutaPlantillas = "/api/programacion/plantillas-turnos";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record TurnoDelCuadroRespuestaSmoke(
        string Id,
        string? Nombre,
        string? Descripcion,
        bool Completo,
        bool Retirado);

    private sealed record DiaDelCuadroRespuestaSmoke(int Dia, TurnoDelCuadroRespuestaSmoke Turno);

    private sealed record CuadroDeTurnosRespuestaSmoke(
        string Id,
        string Nombre,
        int DiasDelCiclo,
        bool Completa,
        IReadOnlyList<DiaDelCuadroRespuestaSmoke> Dias);

    private static string RutaObtener(Guid plantillaId) => $"{RutaPlantillas}/{plantillaId}";

    private async Task<(Guid PlantillaId, string NombreTurno)> CrearPlantillaYAsignarDiaAsync(CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        var turnoId = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Turno Cuadro De Turnos {turnoId}";

        (await _client.PostAsJsonAsync(RutaPlantillas, new
        {
            plantillaId,
            nombre = $"[TEST] Plantilla Cuadro {plantillaId}",
            diasDelCiclo = 7
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que CrearPlantillaTurnos funcione");

        (await _client.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = nombreTurno,
            ordinarias = new[]
            {
                new
                {
                    inicio = "06:00:00",
                    fin = "14:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        }, ct)).IsSuccessStatusCode.Should().BeTrue("el arrange depende de que CrearTurno funcione");

        (await _client.PutAsJsonAsync($"{RutaPlantillas}/{plantillaId}/dias/1", new { turnoId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent,
                "el arrange depende de que AsignarTurnoADiaDePlantillaTurnos funcione");

        return (plantillaId, nombreTurno);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerCuadroDeTurnos_Retorna200ConElDia_CuandoLaPlantillaTieneUnDiaAsignado()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, nombreTurno) = await CrearPlantillaYAsignarDiaAsync(ct);

        var cuadro = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(RutaObtener(plantillaId), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var leido = await response.Content.ReadFromJsonAsync<CuadroDeTurnosRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);
            return leido is { Dias.Count: 1 } ? leido : null;
        }, Timeout);

        cuadro.Id.Should().Be(plantillaId.ToString());
        cuadro.DiasDelCiclo.Should().Be(7);
        cuadro.Completa.Should().BeFalse("solo 1 de los 7 dias del ciclo tiene turno asignado");

        var dia = cuadro.Dias.Should().ContainSingle().Subject;
        dia.Dia.Should().Be(1);
        dia.Turno.Nombre.Should().Be(nombreTurno);
        dia.Turno.Retirado.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerCuadroDeTurnos_Retorna404SinBody_CuandoLaPlantillaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(RutaObtener(Guid.CreateVersion7()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerCuadroDeTurnos_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var response = await _client.GetAsync($"{RutaPlantillas}/no-es-un-guid",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
