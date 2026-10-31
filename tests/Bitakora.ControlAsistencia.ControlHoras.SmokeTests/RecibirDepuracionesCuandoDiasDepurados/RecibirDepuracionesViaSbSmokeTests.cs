using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.RecibirDepuracionesCuandoDiasDepurados;

public class RecibirDepuracionesViaSbSmokeTests(ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private const string TopicDiasDepurados = "dias-depurados";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoEvento = "depuracion_dia_recibida";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string ComputarStreamId(string codigoColaborador, DateOnly fecha) =>
        $"dc:{codigoColaborador}:{fecha:yyyyMMdd}";

    private static DiaDepurado CrearDia(string codigoColaborador, DateOnly fecha) => new(
        codigoColaborador, fecha, null, "[TEST] Turno Lote", [],
        [new MarcacionDelDia(fecha.ToDateTime(new TimeOnly(6, 0)), "ENTRADA")],
        new HorasDiscriminadas(new Dictionary<string, decimal>(), []));

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DiasDepurados_PersisteUnaFotoPorFecha_CuandoElLoteLlegaPorLaCola()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha1 = new DateOnly(2026, 5, 4);
        var fecha2 = new DateOnly(2026, 5, 5);

        await serviceBus.PublishAsync(
            codigoColaborador,
            TopicDiasDepurados,
            new DiasDepurados(codigoColaborador, [CrearDia(codigoColaborador, fecha1), CrearDia(codigoColaborador, fecha2)]),
            Guid.CreateVersion7().ToString());

        foreach (var fecha in new[] { fecha1, fecha2 })
        {
            var streamId = ComputarStreamId(codigoColaborador, fecha);
            var existe = await postgres.ExisteEventoAsync(
                SchemaControlHoras, streamId, TipoEvento, Timeout,
                campoJson: "CodigoColaborador", valorJson: codigoColaborador);

            existe.Should().BeTrue($"el lote deberia dejar {TipoEvento} en el stream {streamId}");
        }
    }
}
