// Arrange via API, nunca sembrando el event store por fuera de ella: el turno se crea con POST
// programacion/turnos, el mismo comando que la proyeccion consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa FichaTurno DESPUES de persistir
// TurnoCreado, por eso el camino feliz va envuelto en Polling.WaitUntilAsync -- unica excepcion
// documentada al "no usar Polling directo en tests".
//
// Alcance black-box: el algoritmo de HorarioResumido/Descripcion y el mapeo de franjas ya los cubre
// el unit test de FichaTurnoProjection; aqui solo el shape basico y los bordes 404/400 contra dev.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ObtenerFichaTurno;

public class ObtenerFichaTurnoSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaTurnos = "/api/programacion/turnos";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La respuesta viaja en camelCase; las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Formas locales DESACOPLADAS del read model de produccion: replican solo el shape JSON de la
    // respuesta. El smoke test no referencia ReadModels ni el Function App (MEF-ADR-0034 seccion 5).
    private sealed record FranjaFichaRespuestaSmoke(
        TimeOnly HoraInicio,
        TimeOnly HoraFin,
        int DiaOffsetFin,
        IReadOnlyList<JsonElement> Descansos,
        IReadOnlyList<JsonElement> Extras,
        string? SedeId,
        string? NombreSede,
        string Descripcion);

    private sealed record FichaTurnoRespuestaSmoke(
        string Id,
        string Nombre,
        bool EsDescanso,
        string HorarioResumido,
        IReadOnlyList<FranjaFichaRespuestaSmoke> Franjas,
        string Descripcion,
        bool Completo);

    private static string Ruta(Guid turnoId) => $"{RutaTurnos}/{turnoId}";

    // El nombre es unico en el catalogo (invariante del dominio): cada caller lo sufija con el
    // turnoId para que estos smoke tests sean re-ejecutables contra el mismo entorno dev.
    private static object PayloadTurnoConFranja(Guid turnoId, string nombre) => new
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

    private static object PayloadDescanso(Guid turnoId, string nombre) => new
    {
        turnoId,
        nombre,
        ordinarias = Array.Empty<object>(),
        esDescanso = true
    };

    // Sin ordinarias y sin esDescanso: el turno nace vacio y se disena por pasos (CA-ADR-0033).
    private static object PayloadTurnoVacio(Guid turnoId, string nombre) => new { turnoId, nombre };

    private async Task CrearTurnoAsync(object payload, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaTurnos, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione");
    }

    // Reintenta el GET hasta que la proyeccion asincrona materialice la ficha (404 = el worker
    // todavia no la aplico). Devuelve un valor no nulo o lanza: ningun caller afirma NotBeNull.
    private Task<FichaTurnoRespuestaSmoke> EsperarFichaAsync(Guid turnoId, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(Ruta(turnoId), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadFromJsonAsync<FichaTurnoRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);
        }, Timeout);

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
    public async Task ObtenerFichaTurno_Retorna200ConElShapeBasico_CuandoElTurnoTieneFranjas()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombre = $"[TEST] Turno Diurno Ficha {turnoId}";

        await CrearTurnoAsync(PayloadTurnoConFranja(turnoId, nombre), ct);

        var ficha = await EsperarFichaAsync(turnoId, ct);

        ficha.Id.Should().Be(turnoId.ToString());
        ficha.Nombre.Should().Be(nombre);
        ficha.EsDescanso.Should().BeFalse();
        ficha.HorarioResumido.Should().NotBeNullOrWhiteSpace();
        ficha.Descripcion.Should().NotBeNullOrWhiteSpace();

        var franja = ficha.Franjas.Should().ContainSingle().Subject;
        franja.HoraInicio.Should().Be(new TimeOnly(6, 0));
        franja.HoraFin.Should().Be(new TimeOnly(14, 0));
        franja.SedeId.Should().BeNull();
        franja.Descansos.Should().BeEmpty();
        franja.Extras.Should().BeEmpty();
        ficha.Completo.Should().BeTrue();
    }

    // CA-2
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaTurno_RetornaEsDescansoTrueYSinFranjas_CuandoElTurnoEsUnDescanso()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombre = $"[TEST] Descanso Ficha {turnoId}";

        await CrearTurnoAsync(PayloadDescanso(turnoId, nombre), ct);

        var ficha = await EsperarFichaAsync(turnoId, ct);

        ficha.EsDescanso.Should().BeTrue();
        ficha.Franjas.Should().BeEmpty();
        ficha.Completo.Should().BeTrue("un descanso es programable aunque no tenga franjas");
    }

    // Cero franjas SIN esDescanso: la ficha lo distingue del descanso -- el mismo conteo, dos
    // respuestas opuestas en EsDescanso y Completo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaTurno_ExponeCompletoFalse_CuandoElTurnoNoTieneFranjas()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        var nombre = $"[TEST] Turno Vacio Ficha {turnoId}";

        await CrearTurnoAsync(PayloadTurnoVacio(turnoId, nombre), ct);

        var ficha = await EsperarFichaAsync(turnoId, ct);

        ficha.EsDescanso.Should().BeFalse();
        ficha.Completo.Should().BeFalse();
        ficha.Franjas.Should().BeEmpty();
    }

    // CA-3
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaTurno_Retorna404SinBody_CuandoLaFichaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(Ruta(Guid.CreateVersion7()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaTurno_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"{RutaTurnos}/no-es-un-guid", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
