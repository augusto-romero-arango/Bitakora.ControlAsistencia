// Arrange via API, nunca sembrando el event store por fuera de ella: cada turno se crea con POST
// programacion/turnos, el mismo comando que la proyeccion consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa FichaTurno DESPUES de persistir
// TurnoCreado, por eso los casos de exito van envueltos en Polling.WaitUntilAsync -- unica
// excepcion documentada al "no usar Polling directo en tests".
//
// Aislamiento SIN cleanup, contra un entorno que ACUMULA fichas de corridas anteriores: cada test
// filtra sus propias filas por Id unico, nunca por conteo ni posicion. El recorrido con cursor usa
// un tenant propio (CA-ADR-0040) para no caminar el catalogo acumulado del tenant compartido.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarFichasTurno;

public class ListarFichasTurnoSmokeTests(ApiFixture api)
{
    private HttpClient _client => api.Client;

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

    private sealed record ListaSmoke(IReadOnlyList<FichaTurnoRespuestaSmoke> Elementos, string? SiguienteCursor);

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

    private Task CrearTurnoAsync(Guid turnoId, string nombre, CancellationToken ct) =>
        CrearTurnoAsync(_client, turnoId, nombre, ct);

    private static async Task CrearTurnoAsync(HttpClient client, Guid turnoId, string nombre, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(RutaTurnos, PayloadTurno(turnoId, nombre), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    // Sin ordinarias y sin esDescanso: el turno nace vacio y se disena por pasos (CA-ADR-0033).
    private async Task CrearTurnoVacioAsync(Guid turnoId, string nombre, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaTurnos, new { turnoId, nombre }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    private async Task AgregarFranjaAsync(Guid turnoId, string inicio, string fin, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"{RutaTurnos}/{turnoId}:agregar-franja", new { inicio, fin }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AgregarFranja funcione");
    }

    private async Task<List<FichaTurnoRespuestaSmoke>> ListarAsync(CancellationToken ct)
    {
        var lista = await ListarPaginaAsync(_client, "", ct);
        lista.SiguienteCursor.Should().BeNull("sin take el endpoint devuelve todo el catalogo");
        return [.. lista.Elementos];
    }

    private static async Task<ListaSmoke> ListarPaginaAsync(HttpClient client, string query, CancellationToken ct)
    {
        var response = await client.GetAsync($"{RutaTurnos}{query}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, cancellationToken: ct);
        return lista!;
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

    // Orden estable por Nombre, verificado por posicion RELATIVA entre dos fichas nuevas
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasTurno_RecorreDosPaginasSinRepetir_CuandoSeUsaTakeYCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tenant = new TenantDeTest(api);
        var turnoIdA = Guid.CreateVersion7();
        var turnoIdB = Guid.CreateVersion7();

        await CrearTurnoAsync(tenant.Client, turnoIdA, $"[TEST] AAA Pagina {turnoIdA}", ct);
        await CrearTurnoAsync(tenant.Client, turnoIdB, $"[TEST] ZZZ Pagina {turnoIdB}", ct);
        await Polling.WaitUntilAsync(async () =>
        {
            var todo = await ListarPaginaAsync(tenant.Client, "", ct);
            return todo.Elementos.Count(f => f.Id == turnoIdA.ToString() || f.Id == turnoIdB.ToString()) == 2
                ? todo
                : null;
        }, Timeout);

        var primera = await ListarPaginaAsync(tenant.Client, "?take=1", ct);
        primera.Elementos.Select(f => f.Id).Should().Equal(turnoIdA.ToString());
        primera.SiguienteCursor.Should().NotBeNullOrEmpty();

        var segunda = await ListarPaginaAsync(
            tenant.Client, $"?take=1&cursor={Uri.EscapeDataString(primera.SiguienteCursor!)}", ct);
        segunda.Elementos.Select(f => f.Id).Should().Equal(turnoIdB.ToString());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasTurno_DevuelveSoloLosTurnosDeLaSede_CuandoSeFiltraPorSedeId()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var otroTurnoId = Guid.CreateVersion7();
        var sedeId = $"SEDE-TEST-{Guid.CreateVersion7()}";

        await CrearTurnoAsync(turnoId, $"[TEST] Turno Con Sede {turnoId}", ct);
        await CrearTurnoAsync(otroTurnoId, $"[TEST] Turno Sin Sede {otroTurnoId}", ct);

        var asignar = await _client.PostAsJsonAsync(
            $"{RutaTurnos}/{turnoId}:asignar-sede-franja",
            new { franja = "06:00", sede = new { id = sedeId, nombre = "[TEST] Sede Filtro" } }, ct);
        asignar.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarSedeAFranja funcione");

        var query = $"?sedeId={Uri.EscapeDataString(sedeId)}";
        var lista = await Polling.WaitUntilAsync(async () =>
        {
            var pagina = await ListarPaginaAsync(_client, query, ct);
            return pagina.Elementos.Any(f => f.Id == turnoId.ToString()) ? pagina : null;
        }, Timeout);

        lista.Elementos.Should().NotBeEmpty();
        foreach (var ficha in lista.Elementos)
        {
            ficha.Franjas.Select(fr => fr.TryGetProperty("sedeId", out var s) ? s.GetString() : null)
                .Should().Contain(sedeId, $"la ficha {ficha.Id} debe tener una franja en la sede filtrada");
        }

        lista.Elementos.Should().NotContain(f => f.Id == otroTurnoId.ToString());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarFichasTurno_Retorna400_CuandoElCursorEsInvalido()
    {
        var response = await _client.GetAsync($"{RutaTurnos}?cursor=no-es-un-cursor",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
