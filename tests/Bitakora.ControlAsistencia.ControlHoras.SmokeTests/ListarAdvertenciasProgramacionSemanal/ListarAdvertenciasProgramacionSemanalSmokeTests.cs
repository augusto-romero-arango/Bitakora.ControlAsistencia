using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ListarAdvertenciasProgramacionSemanal;

public partial class ListarAdvertenciasProgramacionSemanalSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaListado = "/api/control-horas/advertencias-programacion-semanal";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly HttpMethod MetodoQuery = new("QUERY");

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex PatronGuid();

    private async Task<HttpResponseMessage> ConsultarAsync(object filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = JsonContent.Create(filtro)
        };
        return await _client.SendAsync(request, ct);
    }

    private async Task<JsonElement> ConsultarOkAsync(object filtro, CancellationToken ct)
    {
        using var response = await ConsultarAsync(filtro, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.Clone();
    }

    private static HttpClient CrearClienteProgramacion()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = configuration["Programacion:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Programacion:BaseUrl no esta configurado. Usa appsettings.json, appsettings.local.json o la variable Programacion__BaseUrl.");

        var identidad = IdentidadDePrueba.Desde(configuration);
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Add("X-Tenant-Id", identidad.TenantId);
        client.DefaultRequestHeaders.Add("X-User-Id", identidad.UserId);
        client.DefaultRequestHeaders.Add("X-Organization-Membership-Id", identidad.OrganizationMembershipId);
        return client;
    }

    private static async Task<Guid> CrearJornadaAsync(HttpClient programacion, CancellationToken ct)
    {
        var jornadaId = Guid.CreateVersion7();
        var response = await programacion.PostAsJsonAsync("/api/programacion/jornadas", new
        {
            jornadaId,
            horasSemanales = new { horas = 42, minutos = 0 },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct);

        if (response.StatusCode == HttpStatusCode.Created)
            return jornadaId;

        // Los limites son unicos en el catalogo: si ya existe una Jornada 42/8/0/1 el 409 nombra su id.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var cuerpo = await response.Content.ReadAsStringAsync(ct);
        var coincidencia = PatronGuid().Match(cuerpo);
        coincidencia.Success.Should().BeTrue("el 409 deberia nombrar la Jornada existente");
        return Guid.Parse(coincidencia.Value);
    }

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
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna415_CuandoContentTypeNoEsJson()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = new StringContent("{ esto no es json valido", Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna422_CuandoFechaEstaAusente()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { soloConAdvertencias = false }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna422_CuandoElCursorNoSePuedeDecodificar()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            new { fecha = new DateOnly(2026, 10, 7), cursor = "%%%no-es-un-cursor%%%" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAdvertenciasProgramacionSemanal_UbicaLaSemanaIso53_CuandoLaFechaEsElTresDeEneroDe2027()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoInexistente = Guid.CreateVersion7().ToString();

        var respuesta = await ConsultarOkAsync(new
        {
            fecha = new DateOnly(2027, 1, 3),
            codigosColaborador = new[] { codigoInexistente },
            soloConAdvertencias = false
        }, ct);

        respuesta.GetProperty("anioIso").GetInt32().Should().Be(2026);
        respuesta.GetProperty("numeroSemana").GetInt32().Should().Be(53);
        respuesta.GetProperty("desde").GetString().Should().Be("2026-12-28");
        respuesta.GetProperty("hasta").GetString().Should().Be("2027-01-03");
        respuesta.GetProperty("elementos").GetArrayLength().Should().Be(0);
        (!respuesta.TryGetProperty("siguienteCursor", out var cursor)
            || cursor.ValueKind == JsonValueKind.Null).Should().BeTrue();
        respuesta.TryGetProperty("total", out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna200_CuandoTakeEsCero() =>
        await VerificarTakeFueraDeRangoAsync(0);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAdvertenciasProgramacionSemanal_Retorna200_CuandoTakeSuperaElMaximo() =>
        await VerificarTakeFueraDeRangoAsync(500);

    private async Task VerificarTakeFueraDeRangoAsync(int take)
    {
        var ct = TestContext.Current.CancellationToken;

        var respuesta = await ConsultarOkAsync(new
        {
            fecha = new DateOnly(2026, 10, 7),
            codigosColaborador = new[] { Guid.CreateVersion7().ToString() },
            take
        }, ct);

        respuesta.GetProperty("elementos").GetArrayLength().Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAdvertenciasProgramacionSemanal_DebeMostrarAdvertencias_CuandoLaSemanaSuperaElTopeDiario()
    {
        var ct = TestContext.Current.CancellationToken;
        using var programacion = CrearClienteProgramacion();

        var jornadaId = await CrearJornadaAsync(programacion, ct);

        var turnoId = Guid.CreateVersion7();
        var crearTurno = await programacion.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = $"[TEST] Turno 10h Advertencias {turnoId}",
            ordinarias = new[]
            {
                new
                {
                    inicio = "06:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        }, ct);
        crearTurno.StatusCode.Should().Be(HttpStatusCode.Created);

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var lunes = new DateOnly(2026, 11, 2);
        var fechas = Enumerable.Range(0, 7).Select(i => lunes.AddDays(i).ToString("yyyy-MM-dd")).ToArray();

        var solicitud = await programacion.PostAsJsonAsync("/api/programacion/solicitudes", new
        {
            id = Guid.CreateVersion7(),
            turnoId,
            colaborador = new
            {
                identificacion = "CC-866866866",
                codigoColaborador,
                nombreCompleto = "[TEST] Smoke Advertencias Semanales"
            },
            fechas,
            jornadaId
        }, ct);
        solicitud.StatusCode.Should().Be(HttpStatusCode.Created);

        var filtro = new
        {
            fecha = lunes.AddDays(2),
            codigosColaborador = new[] { codigoColaborador }
        };

        JsonElement elemento = default;
        await Polling.WaitUntilTrueAsync(async () =>
        {
            var respuesta = await ConsultarOkAsync(filtro, ct);
            var elementos = respuesta.GetProperty("elementos");
            if (elementos.GetArrayLength() == 0)
                return false;
            elemento = elementos[0].Clone();
            return true;
        }, Timeout);

        elemento.GetProperty("codigoColaborador").GetString().Should().Be(codigoColaborador);

        var casillas = elemento.GetProperty("casillas");
        casillas.GetArrayLength().Should().Be(7);
        foreach (var casilla in casillas.EnumerateArray())
        {
            casilla.GetProperty("advertencias").EnumerateArray()
                .Select(a => a.GetProperty("tipo").GetString())
                .Should().Contain("SuperaTopeDiario");
        }

        var semanales = elemento.GetProperty("advertencias").EnumerateArray().ToArray();
        semanales.Select(a => a.GetProperty("tipo").GetString())
            .Should().Contain(["SuperaHorasSemanales", "FaltanDiasDeDescanso"]);
        semanales.Single(a => a.GetProperty("tipo").GetString() == "FaltanDiasDeDescanso")
            .GetProperty("dias").GetInt32().Should().Be(1);
    }
}
