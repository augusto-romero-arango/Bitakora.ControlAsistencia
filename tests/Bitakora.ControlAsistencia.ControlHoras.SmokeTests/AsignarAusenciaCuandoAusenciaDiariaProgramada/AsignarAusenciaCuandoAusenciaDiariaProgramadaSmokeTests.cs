using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
// Alias de tipo: ResumenColaborador existe homonimo en ControlHoras.DomainEvents (payload por
// rol, MEF-ADR-0039 decision #6); este archivo publica por el bus, asi que usa el de PrivateEvents.
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.AsignarAusenciaCuandoAusenciaDiariaProgramada;

// Consumidor puro (ServiceBusTrigger, sin comando espejo): el Act publica en
// ausencia-diaria-programada el mismo payload plano que emite Programacion, como el precedente
// CancelarTurnoCuandoCancelacionTurnoDiarioSolicitada. Se verifican los dos efectos del handler:
// persistencia de AusenciaDiariaAsignada y publicacion de DiaDepurado con el motivo.
public class AsignarAusenciaCuandoAusenciaDiariaProgramadaSmokeTests(
    ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private const string TopicAusenciaEntrada = "ausencia-diaria-programada";
    private const string SuscripcionConsumidor = "control-horas-escucha-ausencia";
    private const string TopicDiaDepurado = "dia-depurado";
    private const string SuscripcionSmokeTests = "smoke-tests";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoEventoAusenciaDiariaAsignada = "ausencia_diaria_asignada";
    private const string Motivo = "Vacaciones";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string ComputarStreamId(string codigoColaborador, DateOnly fecha) =>
        $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";

    // CA-2 + CA-6: dia sin stream -> la ausencia lo inicia y el DiaDepurado lleva el motivo, sin
    // turno, franjas ni horas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AusenciaDiariaProgramada_AsignaLaAusenciaYPublicaDiaDepuradoConElMotivo_CuandoElDiaNoTieneStream()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 15);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var ausenciaId = Guid.CreateVersion7();
        var colaborador = new ResumenColaborador(
            "CC-746746746", codigoColaborador, "[TEST] Smoke Ausencia Diaria");

        await serviceBus.PurgeAsync(TopicDiaDepurado, SuscripcionSmokeTests);

        await serviceBus.PublishAsync(TopicAusenciaEntrada, new
        {
            AusenciaId = ausenciaId,
            Colaborador = colaborador,
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Motivo
        }, ausenciaId.ToString());

        var asignada = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoAusenciaDiariaAsignada, Timeout,
            campoJson: "AusenciaId", valorJson: ausenciaId.ToString());

        asignada.Should().BeTrue(
            $"el evento {TipoEventoAusenciaDiariaAsignada} deberia existir en el stream {streamId}");

        var diaDepurado = await serviceBus.WaitForMessageAsync<DiaDepurado>(
            TopicDiaDepurado, SuscripcionSmokeTests,
            e => e.CodigoColaborador == codigoColaborador,
            Timeout);

        diaDepurado.Fecha.Should().Be(fecha);
        diaDepurado.MotivoAusencia.Should().Be(Motivo);
        diaDepurado.NombreTurno.Should().BeNull("el dia de ausencia no informa el turno que cubre");
        diaDepurado.Franjas.Should().BeEmpty("con ausencia el dia no se depura contra el turno");
        diaDepurado.HorasDiscriminadas.HorasPorConcepto.Should().BeEmpty("con ausencia no hay horas");

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<AusenciaDiariaProgramadaMinimo>(
            TopicAusenciaEntrada, SuscripcionConsumidor, e => e.AusenciaId == ausenciaId);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (AusenciaId {0}) en '{1}'",
            ausenciaId, SuscripcionConsumidor);
    }
}
