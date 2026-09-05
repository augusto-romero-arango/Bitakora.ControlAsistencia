// Arrange via API, nunca sembrando el event store por fuera de ella: la plantilla se crea con POST
// programacion/plantillas-semanales, el mismo comando que la proyeccion CuadroSemanalTurnos
// consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa (y borra) CuadroSemanalTurnos
// DESPUES de persistir sus eventos, por eso los casos de exito van envueltos en
// Polling.WaitUntilAsync -- unica excepcion documentada al "no usar Polling directo en tests".
//
// Aislamiento SIN cleanup, contra un entorno sin paginacion que ACUMULA plantillas de corridas
// anteriores: cada test filtra sus propias filas por Id unico, nunca por conteo ni posicion.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarCuadrosSemanalesTurnos;

public class ListarCuadrosSemanalesTurnosSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La respuesta viaja en camelCase; las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Forma local DESACOPLADA del DTO de produccion: replica solo el shape JSON de la respuesta
    // HTTP de este endpoint.
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

    private static object PayloadPlantillaValida(Guid plantillaId, string nombre) => new
    {
        plantillaId,
        nombre,
        semanas = 1
    };

    private async Task<(Guid PlantillaId, string Nombre)> CrearPlantillaAsync(CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        var nombre = $"[TEST] Plantilla Listado Cuadro {plantillaId}";

        var response = await _client.PostAsJsonAsync(
            RutaPlantillas, PayloadPlantillaValida(plantillaId, nombre), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaSemanal funcione");

        return (plantillaId, nombre);
    }

    private async Task<List<CuadroSemanalTurnosRespuestaSmoke>> ListarAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync(RutaPlantillas, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<List<CuadroSemanalTurnosRespuestaSmoke>>(
            JsonOptions, cancellationToken: ct);
        return lista ?? [];
    }

    // Reintenta el listado hasta que la proyeccion asincrona satisfaga la condicion.
    private Task<List<CuadroSemanalTurnosRespuestaSmoke>> ListarHastaQueAsync(
        Func<List<CuadroSemanalTurnosRespuestaSmoke>, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var lista = await ListarAsync(ct);
            return condicion(lista) ? lista : null;
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
    public async Task ListarCuadrosSemanalesTurnos_IncluyeElCuadroCreado_CuandoLaPlantillaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, nombre) = await CrearPlantillaAsync(ct);

        var lista = await ListarHastaQueAsync(l => l.Any(c => c.Id == plantillaId.ToString()), ct);

        var cuadro = lista.Should().ContainSingle(c => c.Id == plantillaId.ToString()).Subject;
        cuadro.Nombre.Should().Be(nombre);
        cuadro.Completa.Should().BeFalse("la plantilla no tiene ningun dia asignado");
        cuadro.Dias.Should().BeEmpty();
    }

    // CA-6
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCuadrosSemanalesTurnos_YaNoIncluyeElCuadro_CuandoLaPlantillaFueRetirada()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, _) = await CrearPlantillaAsync(ct);
        await ListarHastaQueAsync(l => l.Any(c => c.Id == plantillaId.ToString()), ct);

        var retiroResponse = await _client.DeleteAsync($"{RutaPlantillas}/{plantillaId}", ct);
        retiroResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que RetirarPlantillaSemanal funcione");

        var lista = await ListarHastaQueAsync(l => l.All(c => c.Id != plantillaId.ToString()), ct);

        lista.Should().NotContain(c => c.Id == plantillaId.ToString());
    }
}
