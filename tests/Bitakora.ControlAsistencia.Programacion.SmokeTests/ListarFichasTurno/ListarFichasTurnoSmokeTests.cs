// Arrange via API, nunca sembrando el event store por fuera de ella: cada turno se crea con POST
// programacion/turnos, el mismo comando que la proyeccion consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa FichaTurno DESPUES de persistir
// TurnoCreado, por eso los casos de exito van envueltos en Polling.WaitUntilAsync -- unica
// excepcion documentada al "no usar Polling directo en tests".
//
// Aislamiento SIN cleanup, contra un entorno sin paginacion que ACUMULA fichas de corridas
// anteriores: cada test filtra sus propias filas por Id unico, nunca por conteo ni posicion.
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

    // La respuesta viaja en camelCase; las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Forma local DESACOPLADA del read model de produccion: replica solo el shape JSON de la
    // respuesta HTTP de este endpoint.
    private sealed record FichaTurnoRespuestaSmoke(
        string Id,
        string Nombre,
        bool EsDescanso,
        string HorarioResumido,
        IReadOnlyList<JsonElement> Franjas,
        string Descripcion,
        bool Completo);

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

    // Sin ordinarias y sin esDescanso: el turno nace vacio y se disena por pasos (CA-ADR-0033).
    private async Task CrearTurnoVacioAsync(Guid turnoId, string nombre, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaTurnos, new { turnoId, nombre }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    private async Task AgregarFranjaAsync(Guid turnoId, string inicio, string fin, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"{RutaTurnos}/{turnoId}:agregar-franja", new { inicio, fin }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AgregarFranja funcione");
    }

    private async Task<List<FichaTurnoRespuestaSmoke>> ListarAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync(RutaTurnos, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<List<FichaTurnoRespuestaSmoke>>(
            JsonOptions, cancellationToken: ct);
        return lista ?? [];
    }

    // Reintenta el listado hasta que la proyeccion asincrona satisfaga la condicion.
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

    // CA-4
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasTurno_IncluyeLaFichaCreada_CuandoSeConsultaElListadoCompleto()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombre = $"[TEST] Turno Listado Ficha {turnoId}";

        await CrearTurnoAsync(turnoId, nombre, ct);

        var lista = await ListarHastaQueAsync(l => l.Any(f => f.Id == turnoId.ToString()), ct);

        var ficha = lista.Should().ContainSingle(f => f.Id == turnoId.ToString()).Subject;
        ficha.Nombre.Should().Be(nombre);
        ficha.EsDescanso.Should().BeFalse();
    }

    // CA-4: orden estable por Nombre, verificado por posicion RELATIVA entre dos fichas nuevas
    // (el listado acumula fichas de corridas anteriores).
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

    // El listado sigue el diseno por pasos: la ficha nace incompleta y la franja agregada despues
    // la vuelve programable sin pasar por CrearTurno de nuevo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasTurno_ReflejaLaFranjaAgregada_CuandoSeDisenaPorPasos()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombre = $"[TEST] Turno Por Pasos Listado {turnoId}";

        await CrearTurnoVacioAsync(turnoId, nombre, ct);
        await ListarHastaQueAsync(l => l.Any(f => f.Id == turnoId.ToString() && !f.Completo), ct);

        await AgregarFranjaAsync(turnoId, "06:00:00", "14:00:00", ct);

        var lista = await ListarHastaQueAsync(
            l => l.Any(f => f.Id == turnoId.ToString() && f.Completo && f.Franjas.Count == 1), ct);

        var ficha = lista.Should().ContainSingle(f => f.Id == turnoId.ToString()).Subject;
        ficha.HorarioResumido.Should().Be("06:00-14:00");
        ficha.EsDescanso.Should().BeFalse();
    }
}
