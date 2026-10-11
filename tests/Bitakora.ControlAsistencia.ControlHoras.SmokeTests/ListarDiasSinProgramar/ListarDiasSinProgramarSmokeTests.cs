using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ListarDiasSinProgramar;

public class ListarDiasSinProgramarSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicEntrada = "programacion-turno-diario-solicitada";
    private const string Ruta = "/api/control-horas/dias-sin-programar";
    private static readonly HttpMethod MetodoQuery = new("QUERY");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record ElementoSmoke(string CodigoColaborador, IReadOnlyList<DateOnly> DiasSinProgramar);

    private sealed record ListaDiasSinProgramarSmoke(
        DateOnly Desde,
        DateOnly Hasta,
        bool RangoRecortado,
        IReadOnlyList<ElementoSmoke> Elementos,
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
            Colaborador = new ResumenColaborador("CC-444555777", codigoColaborador, "[TEST] Smoke Dias Sin Programar"),
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno Dias Sin Programar",
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
                Descripcion = "[TEST] Turno Dias Sin Programar 08:00-16:00"
            }
        };

        await serviceBus.PublishAsync(TopicEntrada, evento, solicitudId.ToString());
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
    public async Task ListarDiasSinProgramar_Retorna415_CuandoContentTypeNoEsJson()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarCrudoAsync("{}", "text/plain", ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDiasSinProgramar_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarCrudoAsync("{ esto no es json valido", "application/json", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDiasSinProgramar_Retorna422_CuandoFaltaColaboradores()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new { desdeFecha = new DateOnly(2027, 3, 1), hastaFecha = new DateOnly(2027, 3, 7) };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDiasSinProgramar_Retorna422_CuandoFaltaDesdeFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new
        {
            hastaFecha = new DateOnly(2027, 3, 7),
            colaboradores = new[] { new { codigoColaborador = "[TEST]", vigenteDesde = new DateOnly(2027, 1, 1) } }
        };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarDiasSinProgramar_DebeListarLosDiasRestantes_CuandoSoloSeProgramaronAlgunosDias()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var lunes = new DateOnly(2027, 3, 1);
        var domingo = lunes.AddDays(6);

        await PublicarTurnoAsync(Guid.CreateVersion7(), codigoColaborador, lunes);
        await PublicarTurnoAsync(Guid.CreateVersion7(), codigoColaborador, lunes.AddDays(1));

        var filtro = new
        {
            desdeFecha = lunes,
            hastaFecha = domingo,
            colaboradores = new[] { new { codigoColaborador, vigenteDesde = new DateOnly(2027, 1, 1) } }
        };

        var esperados = Enumerable.Range(2, 5).Select(lunes.AddDays).ToArray();

        var body = await Polling.WaitUntilAsync(async () =>
        {
            using var response = await ConsultarAsync(filtro, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var lista = await response.Content.ReadFromJsonAsync<ListaDiasSinProgramarSmoke>(
                JsonOptions, cancellationToken: ct);
            var elemento = lista?.Elementos.SingleOrDefault(e => e.CodigoColaborador == codigoColaborador);
            return elemento is not null && elemento.DiasSinProgramar.SequenceEqual(esperados) ? lista : null;
        }, Timeout);

        body.Desde.Should().Be(lunes);
        body.Hasta.Should().Be(domingo);
        body.RangoRecortado.Should().BeFalse();
    }
}
