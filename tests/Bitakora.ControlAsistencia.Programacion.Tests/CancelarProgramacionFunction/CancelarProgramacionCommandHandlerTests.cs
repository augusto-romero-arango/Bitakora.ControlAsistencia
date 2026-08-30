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

    // Identificacion llega ya compuesta como "{Tipo}-{Numero}" y NombreCompleto ya concatenado:
    // los compone el cliente contra el maestro Colaboradores, nunca este servidor.
    private static readonly ColaboradorSolicitado Colaborador =
        new("CC-12345678", "E001", "Juan Perez");

    // Los mismos tres valores en los otros dos roles de payload -- bus (PrivateEvents) y evento
    // persistido (Programacion.DomainEvents): tres islas, un tipo por rol (MEF-ADR-0039 decision 2).
    private static readonly ResumenColaborador ColaboradorResumen =
        new("CC-12345678", "E001", "Juan Perez");

    private static readonly ColaboradorProgramado ColaboradorProgramadoEsperado =
        new("CC-12345678", "E001", "Juan Perez");

    protected override ICommandHandlerAsync<CancelarProgramacion> Handler =>
        new CancelarProgramacionCommandHandler(EventStore, PrivateEventSender);

    // CA-1
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

    // CA-1
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

    // CA-1: delata que alguno de los dos mapeos componga o permute un campo de la terna.
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

    // CA-2: el aggregate declina con excepcion que el endpoint traduce a 409, sin evento de fallo
    // persistido -- no hay consumidor downstream que reaccione (CA-ADR-0030).
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
