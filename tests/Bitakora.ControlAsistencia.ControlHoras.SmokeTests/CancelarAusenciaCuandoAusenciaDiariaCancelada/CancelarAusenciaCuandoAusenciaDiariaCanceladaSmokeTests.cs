using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
// Alias de tipo: ResumenColaborador existe homonimo en ControlHoras.DomainEvents (payload por
// rol, MEF-ADR-0039 decision #6); este archivo publica por el bus, asi que usa el de PrivateEvents.
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.CancelarAusenciaCuandoAusenciaDiariaCancelada;

// Consumidor puro (ServiceBusTrigger, sin comando espejo): el Act publica en los topics de entrada
// el mismo payload plano que emite Programacion, como el precedente
// AsignarAusenciaCuandoAusenciaDiariaProgramada. Se verifican los dos efectos del handler:
// persistencia de la cancelacion y publicacion de DiaDepurado sin motivo.
public class CancelarAusenciaCuandoAusenciaDiariaCanceladaSmokeTests(
    ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private const string TopicAusenciaProgramada = "ausencia-diaria-programada";
    private const string TopicAusenciaCancelada = "ausencia-diaria-cancelada";
    private const string SuscripcionConsumidor = "control-horas-escucha-cancelacion-ausencia";
    private const string TopicDiaDepurado = "dia-depurado";
    private const string SuscripcionSmokeTests = "smoke-tests";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoEventoCancelacionRegistrada = "cancelacion_ausencia_diaria_registrada";
    private const string Motivo = "Vacaciones";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string ComputarStreamId(string codigoColaborador, DateOnly fecha) =>
        $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";

    // CA-7 (lado consumidor): el endpoint de cancelacion de Programacion (#744) aun no existe, asi
    // que el smoke publica directamente en los topics que ese flujo alimenta.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AusenciaDiariaCancelada_LiberaElDiaYPublicaDiaDepuradoSinMotivo_CuandoLaAusenciaVigenteEsLaCancelada()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 16);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var ausenciaId = Guid.CreateVersion7();
        var colaborador = new ResumenColaborador(
            "CC-747747747", codigoColaborador, "[TEST] Smoke Cancelar Ausencia Diaria");

        await serviceBus.PurgeAsync(TopicDiaDepurado, SuscripcionSmokeTests);

        await serviceBus.PublishAsync(TopicAusenciaProgramada, new
        {
            AusenciaId = ausenciaId,
            Colaborador = colaborador,
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Motivo
        }, ausenciaId.ToString());

        var conMotivo = await serviceBus.WaitForMessageAsync<DiaDepurado>(
            TopicDiaDepurado, SuscripcionSmokeTests,
            e => e.CodigoColaborador == codigoColaborador && e.MotivoAusencia == Motivo,
            Timeout);
        conMotivo.Fecha.Should().Be(fecha);

        await serviceBus.PublishAsync(TopicAusenciaCancelada, new
        {
            AusenciaId = ausenciaId,
            Colaborador = colaborador,
            Fecha = fecha.ToString("yyyy-MM-dd")
        }, ausenciaId.ToString());

        var cancelada = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoCancelacionRegistrada, Timeout,
            campoJson: "AusenciaId", valorJson: ausenciaId.ToString());

        cancelada.Should().BeTrue(
            $"el evento {TipoEventoCancelacionRegistrada} deberia existir en el stream {streamId}");

        var sinMotivo = await serviceBus.WaitForMessageAsync<DiaDepurado>(
            TopicDiaDepurado, SuscripcionSmokeTests,
            e => e.CodigoColaborador == codigoColaborador && e.MotivoAusencia is null,
            Timeout);

        sinMotivo.Fecha.Should().Be(fecha);
        sinMotivo.NombreTurno.Should().BeNull("el dia no tenia turno: queda sin programar");

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<AusenciaDiariaCanceladaMinimo>(
            TopicAusenciaCancelada, SuscripcionConsumidor, e => e.AusenciaId == ausenciaId);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber un dead letter de esta corrida (AusenciaId {0}) en '{1}'",
            ausenciaId, SuscripcionConsumidor);
    }
}
