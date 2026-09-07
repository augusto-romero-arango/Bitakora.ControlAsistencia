// Arrange via API, nunca sembrando el event store por fuera de ella: la plantilla se crea con POST
// programacion/plantillas-semanales, el turno con POST programacion/turnos y el dia se asigna con
// PUT .../dias/{semana}/{dia} -- los mismos comandos que las proyecciones consumen.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa CuadroSemanalTurnos y FichaTurno
// DESPUES de persistir sus eventos, por eso los casos de exito van envueltos en
// Polling.WaitUntilAsync -- unica excepcion documentada al "no usar Polling directo en tests".
//
// Alcance black-box: la composicion Completa/Retirado ya la cubre CuadroSemanalTurnosRespuestaTests
// (CA-1..CA-3); aqui solo el shape basico y los bordes 404/400 contra dev.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ObtenerCuadroSemanalTurnos;

public class ObtenerCuadroSemanalTurnosSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string RutaTurnos = "/api/programacion/turnos";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La respuesta viaja en camelCase; las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Formas locales DESACOPLADAS del DTO de produccion: replican solo el shape JSON de la
    // respuesta. El smoke test no referencia el namespace del endpoint ni ReadModels.
    private sealed record TurnoDelCuadroRespuestaSmoke(
        string Id,
        string? Nombre,
        string? Descripcion,
        bool Completo,
        bool Retirado);

    private sealed record DiaDelCuadroRespuestaSmoke(int Semana, int Dia, TurnoDelCuadroRespuestaSmoke Turno);

    private sealed record CuadroSemanalTurnosRespuestaSmoke(
        string Id,
        string Nombre,
        int Semanas,
        bool Completa,
        IReadOnlyList<DiaDelCuadroRespuestaSmoke> Dias);

    private static string RutaObtener(Guid plantillaId) => $"{RutaPlantillas}/{plantillaId}";

    private static object PayloadPlantillaValida(Guid plantillaId, string nombre, int semanas) => new
    {
        plantillaId,
        nombre,
        semanas
    };

    private static object PayloadTurnoValido(Guid turnoId, string nombre) => new
    {
        turnoId,
        nombre,
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
    };

    // Plantilla de 1 semana con un unico dia (1/1) asignado -- 1 de los 7 dias, suficiente para
    // ejercitar la composicion sin agotar el catalogo ISO completo (CA-5 del issue #625).
    private async Task<(Guid PlantillaId, Guid TurnoId, string NombreTurno)> CrearPlantillaTurnoYAsignarDiaAsync(
        CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        var turnoId = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Turno Cuadro {turnoId}";

        var plantillaResponse = await _client.PostAsJsonAsync(
            RutaPlantillas,
            PayloadPlantillaValida(plantillaId, $"[TEST] Plantilla Cuadro {plantillaId}", semanas: 1),
            ct);
        plantillaResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaSemanal funcione");

        var turnoResponse = await _client.PostAsJsonAsync(
            RutaTurnos, PayloadTurnoValido(turnoId, nombreTurno), ct);
        turnoResponse.IsSuccessStatusCode.Should().BeTrue(
            "el arrange de este smoke test depende de que CrearTurno acepte el comando");

        var asignarResponse = await _client.PutAsJsonAsync(
            $"{RutaPlantillas}/{plantillaId}/dias/1/1", new { turnoId }, ct);
        asignarResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarTurnoADia funcione");

        return (plantillaId, turnoId, nombreTurno);
    }

    // Reintenta el GET hasta que la proyeccion asincrona satisfaga la condicion (404 = el worker
    // todavia no la aplico, o la condicion sobre el cuerpo aun no se cumple).
    private Task<CuadroSemanalTurnosRespuestaSmoke> EsperarCuadroAsync(
        Guid plantillaId, Func<CuadroSemanalTurnosRespuestaSmoke, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(RutaObtener(plantillaId), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var cuadro = await response.Content.ReadFromJsonAsync<CuadroSemanalTurnosRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);
            return cuadro is not null && condicion(cuadro) ? cuadro : null;
        }, Timeout);

    private Task<bool> EsperarQueDesaparezcaAsync(Guid plantillaId, CancellationToken ct) =>
        Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync(RutaObtener(plantillaId), ct);
            return response.StatusCode == HttpStatusCode.NotFound;
        }, Timeout);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-5
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerCuadroSemanalTurnos_DebeRetornar200ConElCuadroResuelto_CuandoLaPlantillaTieneUnDiaAsignado()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, _, nombreTurno) = await CrearPlantillaTurnoYAsignarDiaAsync(ct);

        var cuadro = await EsperarCuadroAsync(plantillaId, c => c.Dias.Count == 1, ct);

        cuadro.Id.Should().Be(plantillaId.ToString());
        cuadro.Semanas.Should().Be(1);
        cuadro.Completa.Should().BeFalse("solo 1 de los 7 dias de la semana tiene turno asignado");

        var dia = cuadro.Dias.Should().ContainSingle().Subject;
        dia.Semana.Should().Be(1);
        dia.Dia.Should().Be(1);
        dia.Turno.Nombre.Should().Be(nombreTurno);
        dia.Turno.Retirado.Should().BeFalse();
    }

    // CA-5
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerCuadroSemanalTurnos_Retorna404SinBody_CuandoLaPlantillaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(RutaObtener(Guid.CreateVersion7()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    // CA-5
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerCuadroSemanalTurnos_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"{RutaPlantillas}/no-es-un-guid", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerCuadroSemanalTurnos_MarcaElTurnoRetiradoYBorraElCuadroRetirado()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, turnoId, _) = await CrearPlantillaTurnoYAsignarDiaAsync(ct);
        await EsperarCuadroAsync(plantillaId, c => c.Dias.Count == 1, ct);

        var retiroTurnoResponse = await _client.DeleteAsync($"{RutaTurnos}/{turnoId}", ct);
        retiroTurnoResponse.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RetirarTurno funcione");

        var cuadroConTurnoRetirado = await EsperarCuadroAsync(
            plantillaId, c => c.Dias.Single().Turno.Retirado, ct);
        cuadroConTurnoRetirado.Dias.Single().Turno.Nombre.Should().BeNull();
        cuadroConTurnoRetirado.Completa.Should().BeFalse();

        var retiroPlantillaResponse = await _client.DeleteAsync(RutaObtener(plantillaId), ct);
        retiroPlantillaResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que RetirarPlantillaSemanal funcione");

        var quedoNotFound = await EsperarQueDesaparezcaAsync(plantillaId, ct);
        quedoNotFound.Should().BeTrue("el cuadro deberia desaparecer del read-side tras retirar la plantilla");
    }
}
