// Arrange via API, nunca sembrando el event store por fuera de ella: la plantilla se crea con POST
// programacion/plantillas-semanales, el mismo comando que la proyeccion CuadroSemanalTurnos
// consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa (y borra) CuadroSemanalTurnos
// DESPUES de persistir sus eventos, por eso los casos de exito van envueltos en
// Polling.WaitUntilAsync -- unica excepcion documentada al "no usar Polling directo en tests".
//
// Aislamiento SIN cleanup: cada test corre en un tenant propio (CA-ADR-0040) y filtra sus propias
// filas por Id unico, nunca por conteo ni posicion.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarCuadrosSemanalesTurnos;

public class ListarCuadrosSemanalesTurnosSmokeTests(ApiFixture api) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private HttpClient _client => _tenant.Client;

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
        IReadOnlyList<DiaDelCuadroRespuestaSmoke> Dias,
        JornadaDelCuadroRespuestaSmoke? Jornada = null,
        IReadOnlyList<AdvertenciaDelCuadroRespuestaSmoke>? Advertencias = null);

    private sealed record ListaSmoke(
        IReadOnlyList<CuadroSemanalTurnosRespuestaSmoke> Elementos, string? SiguienteCursor);

    private sealed record HorasYMinutosRespuestaSmoke(int Horas, int Minutos);

    private sealed record JornadaDelCuadroRespuestaSmoke(
        string Id,
        HorasYMinutosRespuestaSmoke HorasSemanales,
        string? Descripcion);

    private sealed record AdvertenciaDelCuadroRespuestaSmoke(
        string Tipo, int? Semana, int? Dia, string? Descripcion);

    private static object PayloadPlantillaValida(Guid plantillaId, string nombre) => new
    {
        plantillaId,
        nombre,
        semanas = 1
    };

    private Task<(Guid PlantillaId, string Nombre)> CrearPlantillaAsync(CancellationToken ct) =>
        CrearPlantillaAsync("[TEST] Plantilla Listado Cuadro", ct);

    private async Task<(Guid PlantillaId, string Nombre)> CrearPlantillaAsync(string prefijo, CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        var nombre = $"{prefijo} {plantillaId}";

        var response = await _client.PostAsJsonAsync(
            RutaPlantillas, PayloadPlantillaValida(plantillaId, nombre), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaSemanal funcione");

        return (plantillaId, nombre);
    }

    private async Task<List<CuadroSemanalTurnosRespuestaSmoke>> ListarAsync(CancellationToken ct)
    {
        var lista = await ListarPaginaAsync("", ct);
        lista.SiguienteCursor.Should().BeNull("sin take el endpoint devuelve todo el catalogo");
        return [.. lista.Elementos];
    }

    private async Task<ListaSmoke> ListarPaginaAsync(string query, CancellationToken ct)
    {
        var response = await _client.GetAsync($"{RutaPlantillas}{query}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, cancellationToken: ct);
        return lista!;
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCuadrosSemanalesTurnos_IncluyeJornadaYAdvertencias_CuandoLaPlantillaTieneJornada()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = Guid.CreateVersion7();
        var jornadaId = Guid.CreateVersion7();
        var firma = LimitesDePrueba.ListarCuadros;

        (await _client.PostAsJsonAsync("/api/programacion/jornadas", firma.PayloadCrear(jornadaId), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que CrearJornada funcione");
        (await _client.PostAsJsonAsync(RutaPlantillas,
            PayloadPlantillaValida(plantillaId, $"[TEST] Plantilla Listado Jornada {plantillaId}"), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created,
                "el arrange depende de que CrearPlantillaSemanal funcione");
        (await _client.PutAsJsonAsync($"{RutaPlantillas}/{plantillaId}/jornada", new { jornadaId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent,
                "el arrange depende de que AsignarJornadaAPlantillaSemanal funcione");

        var lista = await ListarHastaQueAsync(
            l => l.Any(c => c.Id == plantillaId.ToString() && c.Jornada is not null
                && c.Advertencias is { Count: > 0 }),
            ct);

        var cuadro = lista.Single(c => c.Id == plantillaId.ToString());
        cuadro.Jornada!.Id.Should().Be(jornadaId.ToString());
        cuadro.Jornada.HorasSemanales.Should().Be(
            new HorasYMinutosRespuestaSmoke(firma.SemanalesHoras, firma.SemanalesMinutos));
        cuadro.Advertencias!.Should().Contain(a => a.Tipo == "DiaSinTurno");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCuadrosSemanalesTurnos_RecorreDosPaginasSinRepetir_CuandoSeUsaTakeYCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (primeraId, _) = await CrearPlantillaAsync("[TEST] AAA Pagina Cuadro", ct);
        var (segundaId, _) = await CrearPlantillaAsync("[TEST] ZZZ Pagina Cuadro", ct);
        await ListarHastaQueAsync(
            l => l.Any(c => c.Id == primeraId.ToString()) && l.Any(c => c.Id == segundaId.ToString()), ct);

        var primera = await ListarPaginaAsync("?take=1", ct);
        primera.Elementos.Select(c => c.Id).Should().Equal(primeraId.ToString());
        primera.SiguienteCursor.Should().NotBeNullOrEmpty();

        var segunda = await ListarPaginaAsync(
            $"?take=1&cursor={Uri.EscapeDataString(primera.SiguienteCursor!)}", ct);
        segunda.Elementos.Select(c => c.Id).Should().Equal(segundaId.ToString());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCuadrosSemanalesTurnos_Retorna400_CuandoElCursorEsInvalido()
    {
        var response = await _client.GetAsync($"{RutaPlantillas}?cursor=no-es-un-cursor",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
