// Issue #496: smoke tests de ListarFichasTurno, GET programacion/turnos -- mismo recurso que
// CrearTurno (POST), sin ningun filtro server-side (catalogo acotado, decenas por empresa) ni
// paginacion, con orden estable por Nombre (desempate por Id) como contrato de la respuesta
// (MEF-ADR-0042 seccion 1, CA-4).
//
// Arrange via API, nunca sembrando el event store por fuera de ella: cada turno se crea con POST
// programacion/turnos -- el mismo comando que la proyeccion consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa FichaTurno DESPUES de que
// CatalogoTurnos persiste TurnoCreado. Los casos de exito envuelven la consulta en
// Polling.WaitUntilAsync (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling
// directo en tests".
//
// Aislamiento de datos SIN cleanup, en un entorno SIN paginacion que ACUMULA fichas de corridas
// anteriores: cada test filtra sus propias filas por Id unico (Guid), nunca por conteo total ni
// posicion/indice.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarFichasTurno;

public class ListarFichasTurnoSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaTurnos = "/api/programacion/turnos";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase, mientras que las formas locales de este
    // archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Forma local DESACOPLADA del read model de produccion (ReadModels.Programacion.FichaTurno):
    // replica solo el shape JSON de la respuesta HTTP de este endpoint.
    private sealed record FichaTurnoRespuestaSmoke(
        string Id,
        string Nombre,
        bool EsDescanso,
        string HorarioResumido,
        IReadOnlyList<JsonElement> Franjas,
        string Descripcion);

    private static object PayloadTurno(Guid turnoId, string nombre) => new
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

    private async Task CrearTurnoAsync(Guid turnoId, string nombre, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaTurnos, PayloadTurno(turnoId, nombre), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    private async Task<List<FichaTurnoRespuestaSmoke>> ListarAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync(RutaTurnos, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<List<FichaTurnoRespuestaSmoke>>(
            JsonOptions, cancellationToken: ct);
        return lista ?? [];
    }

    // Reintenta el listado hasta que la proyeccion asincrona satisfaga la condicion -- unica
    // excepcion documentada al "no usar Polling directo en tests" (lifecycle Async, MEF-ADR-0034
    // seccion 3).
    private Task<List<FichaTurnoRespuestaSmoke>> ListarHastaQueAsync(
        Func<List<FichaTurnoRespuestaSmoke>, bool> condicion, CancellationToken ct) =>
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

    // CA-4: el turno recien creado aparece en el listado completo (sin filtro), filtrando por su Id
    // unico -- nunca por posicion/indice, en un entorno que acumula fichas de corridas anteriores.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasTurno_IncluyeLaFichaCreada_CuandoSeConsultaElListadoCompleto()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        const string nombre = "[TEST] Turno Listado Ficha";

        await CrearTurnoAsync(turnoId, nombre, ct);

        var lista = await ListarHastaQueAsync(l => l.Any(f => f.Id == turnoId.ToString()), ct);

        var ficha = lista.Should().ContainSingle(f => f.Id == turnoId.ToString()).Subject;
        ficha.Nombre.Should().Be(nombre);
        ficha.EsDescanso.Should().BeFalse();
    }

    // CA-4: orden estable por Nombre -- dos turnos nuevos deben aparecer en el listado en el mismo
    // orden relativo que sus nombres (comparacion ordinal).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasTurno_OrdenaPorNombre_CuandoHayVariasFichasNuevas()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreA = $"[TEST] AAA Orden {sufijo}";
        var nombreB = $"[TEST] ZZZ Orden {sufijo}";
        var turnoIdA = Guid.CreateVersion7();
        var turnoIdB = Guid.CreateVersion7();

        await CrearTurnoAsync(turnoIdA, nombreA, ct);
        await CrearTurnoAsync(turnoIdB, nombreB, ct);

        var lista = await ListarHastaQueAsync(
            l => l.Any(f => f.Id == turnoIdA.ToString()) && l.Any(f => f.Id == turnoIdB.ToString()),
            ct);

        var indiceA = lista.FindIndex(f => f.Id == turnoIdA.ToString());
        var indiceB = lista.FindIndex(f => f.Id == turnoIdB.ToString());

        indiceA.Should().BeLessThan(indiceB,
            "el orden estable es por Nombre, y nombreA precede a nombreB alfabeticamente");
    }
}
