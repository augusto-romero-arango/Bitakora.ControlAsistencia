using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ListarMallaSemanalTurnos;

public class ListarMallaSemanalTurnosSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicEntrada = ProgramacionEnLote.Topic;
    private const string Ruta = "/api/control-horas/malla-semanal-turnos";
    private static readonly HttpMethod MetodoQuery = new("QUERY");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record CasillaSmoke(DateOnly Fecha, string Tipo);

    private sealed record FilaSmoke(string CodigoColaborador, IReadOnlyList<CasillaSmoke> Casillas);

    private sealed record MallaSmoke(
        DateOnly Desde,
        DateOnly Hasta,
        int AnioIso,
        int NumeroSemana,
        IReadOnlyList<FilaSmoke> Elementos,
        string? SiguienteCursor);

    private async Task<HttpResponseMessage> ConsultarAsync(object filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, Ruta) { Content = JsonContent.Create(filtro) };
        return await _client.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> ConsultarCrudoAsync(string body, string contentType, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, Ruta)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        };
        return await _client.SendAsync(request, ct);
    }

    private async Task PublicarTurnoAsync(Guid solicitudId, string codigoColaborador, DateOnly fecha)
    {
        var evento = new
        {
            SolicitudId = solicitudId,
            Colaborador = new ResumenColaborador("CC-444555888", codigoColaborador, "[TEST] Smoke Malla Semanal"),
            Fechas = new[] { fecha.ToString("yyyy-MM-dd") },
            Jornada = ProgramacionEnLote.Jornada,
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno Malla Semanal",
                FranjasOrdinarias = new object[]
                {
                    new
                    {
                        HoraInicio = "08:00:00",
                        HoraFin = "16:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>(),
                        Descripcion = (string?)null
                    }
                },
                Descripcion = "[TEST] Turno Malla Semanal 08:00-16:00"
            }
        };

        await serviceBus.PublishAsync(codigoColaborador, TopicEntrada, evento, solicitudId.ToString());
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
    public async Task ListarMallaSemanalTurnos_Retorna422_CuandoFaltaFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            colaboradores = new[] { new { codigoColaborador = "[TEST]", vigenteDesde = new DateOnly(2027, 1, 1) } }
        };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarMallaSemanalTurnos_Retorna422_CuandoElCursorEsInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            fecha = new DateOnly(2027, 3, 3),
            colaboradores = new[] { new { codigoColaborador = "[TEST]", vigenteDesde = new DateOnly(2027, 1, 1) } },
            cursor = "!!!cursor-invalido!!!"
        };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarMallaSemanalTurnos_Retorna415_CuandoContentTypeNoEsJson()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarCrudoAsync("{}", "text/plain", ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarMallaSemanalTurnos_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarCrudoAsync("{ esto no es json valido", "application/json", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarMallaSemanalTurnos_DebeComponerLaFila_CuandoSoloSeProgramoElLunes()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var lunes = new DateOnly(2027, 3, 1);

        await PublicarTurnoAsync(Guid.CreateVersion7(), codigoColaborador, lunes);

        var filtro = new
        {
            fecha = new DateOnly(2027, 3, 3),
            colaboradores = new[]
            {
                new { codigoColaborador, vigenteDesde = new DateOnly(2027, 1, 1), vigenteHasta = (DateOnly?)new DateOnly(2027, 3, 5) }
            }
        };

        var tiposEsperados = new[]
        {
            "Trabajo", "SinProgramar", "SinProgramar", "SinProgramar", "SinProgramar",
            "FueraDeVinculacion", "FueraDeVinculacion"
        };

        MallaSmoke? malla = null;
        var completa = await Polling.WaitUntilTrueAsync(async () =>
        {
            using var response = await ConsultarAsync(filtro, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            malla = await response.Content.ReadFromJsonAsync<MallaSmoke>(JsonOptions, cancellationToken: ct);
            var fila = malla?.Elementos.SingleOrDefault(e => e.CodigoColaborador == codigoColaborador);
            return fila is not null && fila.Casillas[0].Tipo == "Trabajo";
        }, Timeout);

        completa.Should().BeTrue("el turno del lunes publicado en el arrange debe reflejarse en la malla");

        malla!.Desde.Should().Be(lunes);
        malla.Hasta.Should().Be(lunes.AddDays(6));

        var casillas = malla.Elementos.Single(e => e.CodigoColaborador == codigoColaborador).Casillas;
        casillas.Select(c => c.Fecha).Should().Equal(Enumerable.Range(0, 7).Select(lunes.AddDays));
        casillas.Select(c => c.Tipo).Should().Equal(tiposEsperados);
    }
}
