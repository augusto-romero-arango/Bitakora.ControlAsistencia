// Issue #498: Cancelar la programacion de dias especificos de un colaborador. Espejo estructural
// de SolicitarProgramacionTurnoCommandHandlerTests: mismo patron de idempotencia (Id duplicado ->
// 409) y de N eventos de bus (uno por fecha), sin cascada de sede ni consulta al catalogo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction;
using Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarProgramacionFunction;

public class CancelarProgramacionCommandHandlerTests
    : CommandHandlerAsyncTest<CancelarProgramacion>
{
    private static readonly DateOnly Fecha1 = new(2026, 4, 7);
    private static readonly DateOnly Fecha2 = new(2026, 4, 8);

    // La terna de identidad tal como llega en el body: Identificacion ya compuesta como
    // "{Tipo}-{Numero}" y NombreCompleto ya concatenado por el cliente (mismo contrato que
    // SolicitarProgramacionTurno, #436).
    private static readonly ColaboradorSolicitado Colaborador =
        new("CC-12345678", "E001", "Juan Perez");

    // Los mismos tres valores en los otros dos roles de payload (tres islas, MEF-ADR-0039 decision
    // 2): el del bus (PrivateEvents) y el del evento persistido (Programacion.DomainEvents).
    private static readonly ResumenColaborador ColaboradorResumen =
        new("CC-12345678", "E001", "Juan Perez");

    private static readonly ColaboradorProgramado ColaboradorProgramadoEsperado =
        new("CC-12345678", "E001", "Juan Perez");

    protected override ICommandHandlerAsync<CancelarProgramacion> Handler =>
        new CancelarProgramacionCommandHandler(EventStore, PrivateEventSender);

    // CA-1: una solicitud valida persiste CancelacionProgramacionSolicitada en un stream nuevo y
    // publica una CancelacionTurnoDiarioSolicitada por la fecha.
    [Fact]
    public async Task CancelarProgramacion_EmiteCancelacionSolicitadaYPublicaEvento_CuandoDatosValidos()
    {
        Given();
        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Fecha1]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1]));
        ThenIsPublishedPrivately(new CancelacionTurnoDiarioSolicitada(
            GuidAggregateId, ColaboradorResumen, Fecha1));
        And<SolicitudCancelacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    // CA-1: N fechas producen N eventos de bus, uno por fecha.
    [Fact]
    public async Task CancelarProgramacion_PublicaUnEventoPorCadaFecha_CuandoHayMultiplesFechas()
    {
        Given();
        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Fecha1, Fecha2]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1, Fecha2]));
        ThenIsPublishedPrivately(
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Fecha1),
            new CancelacionTurnoDiarioSolicitada(GuidAggregateId, ColaboradorResumen, Fecha2));
        And<SolicitudCancelacionAggregateRoot, int>(s => s.Fechas.Count, 2);
    }

    // CA-1: la terna de identidad fluye TAL CUAL a los dos payloads (persistido y de bus), sin
    // componer ni permutar ningun campo.
    [Fact]
    public async Task CancelarProgramacion_PersisteLaTernaDeIdentidadDelColaborador_CuandoDatosValidos()
    {
        Given();
        await WhenAsync(new CancelarProgramacion(GuidAggregateId, Colaborador, [Fecha1]));

        Then(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1]));
        ThenIsPublishedPrivately(new CancelacionTurnoDiarioSolicitada(
            GuidAggregateId, ColaboradorResumen, Fecha1));
        And<SolicitudCancelacionAggregateRoot, ColaboradorProgramado?>(
            s => s.Colaborador, ColaboradorProgramadoEsperado);
    }

    // CA-2: idempotencia -- solicitud ya existente (mismo Id) lanza excepcion que el endpoint mapea
    // a 409 (CA-ADR-0030, mismo trato que SolicitudYaExiste en programar).
    [Fact]
    public async Task CancelarProgramacion_LanzaInvalidOperationException_CuandoSolicitudYaExiste()
    {
        Given(new CancelacionProgramacionSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1]));

        var act = async () => await WhenAsync(
            new CancelarProgramacion(GuidAggregateId, Colaborador, [Fecha1]));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{CancelarProgramacionCommandHandler.Mensajes.SolicitudYaExiste}*");
        And<SolicitudCancelacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }
}
