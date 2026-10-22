using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.RepartirMarcacionesCuandoDiaDepuradoFunction;

public class RepartirMarcacionesSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaMarcaciones = "/api/control-horas/marcaciones";
    private const string TopicProgramacionEntrada = "programacion-turno-diario-solicitada";
    private const string TopicDiaDepurado = "dia-depurado";
    private const string SuscripcionReparto = "control-horas-reparte-marcaciones";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record FranjaDepuradaSmoke(DateTime? Entrada, DateTime? Salida, bool EsAnomala);

    private sealed record DepuracionDelDiaSmoke(IReadOnlyList<FranjaDepuradaSmoke> Franjas);

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
    public async Task RepartirMarcaciones_EntregaLaSalidaDeMadrugadaALaVispera_CuandoTurnoNocturnoYMarcacionesRegistradas()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var solicitudId = Guid.CreateVersion7();
        var vispera = new DateOnly(2026, 6, 20);
        var diaSiguiente = vispera.AddDays(1);
        var entrada = vispera.ToDateTime(new TimeOnly(21, 55));
        var salida = diaSiguiente.ToDateTime(new TimeOnly(6, 5));

        var turno = new
        {
            SolicitudId = solicitudId,
            Colaborador = new
            {
                Identificacion = "CC-777666555",
                CodigoColaborador = codigoColaborador,
                NombreCompleto = "[TEST] Smoke Reparto"
            },
            Fecha = vispera.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[TEST] Turno Nocturno Reparto",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = "22:00:00",
                        HoraFin = "06:00:00",
                        DiaOffsetFin = 1,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>()
                    }
                }
            }
        };

        await serviceBus.PublishAsync(TopicProgramacionEntrada, turno, Guid.CreateVersion7().ToString());

        foreach (var momento in new[] { entrada, salida })
        {
            var response = await _client.PostAsJsonAsync(RutaMarcaciones, new
            {
                codigoColaborador,
                timestamp = momento.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
                tipoMarcacion = "ENTRADA",
                dispositivoId = "[TEST] DEV-SMOKE-REPARTO"
            }, ct);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // ObtenerDepuracionDelDia lee el dc: que RecibirDepuracionCuandoDiaDepurado construye desde el
        // DiaDepurado publicado: la salida en la vispera cubre por transitividad la copia persistida
        // por el reparto y su publicacion.
        var ruta = $"/api/control-horas/depuraciones/{codigoColaborador}/{vispera:yyyy-MM-dd}";
        DepuracionDelDiaSmoke? vista = null;
        var completo = await Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            if (response.StatusCode != HttpStatusCode.OK)
                return false;

            vista = await response.Content.ReadFromJsonAsync<DepuracionDelDiaSmoke>(JsonOptions, cancellationToken: ct);
            return vista?.Franjas.Any(f => f.Entrada == entrada && f.Salida == salida) == true;
        }, Timeout);

        completo.Should().BeTrue(
            "la depuracion de la vispera debe reflejar la entrada 21:55 y la salida 06:05 entregada por el reparto");

        vista!.Franjas.Should().ContainSingle()
            .Which.Should().Match<FranjaDepuradaSmoke>(f => !f.EsAnomala);

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<DiaDepuradoMinimo>(
            TopicDiaDepurado, SuscripcionReparto, e => e.CodigoColaborador == codigoColaborador);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (CodigoColaborador {0}) en '{1}' - si lo hay, el reparto fallo",
            codigoColaborador, SuscripcionReparto);
    }
}
