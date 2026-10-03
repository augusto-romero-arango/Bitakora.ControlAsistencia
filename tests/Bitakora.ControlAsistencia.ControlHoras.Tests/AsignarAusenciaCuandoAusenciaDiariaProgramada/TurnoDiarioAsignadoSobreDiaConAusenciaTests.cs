using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using DiaDepurado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiaDepurado;
using HorasDiscriminadas = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.HorasDiscriminadas;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarAusenciaCuandoAusenciaDiariaProgramada;

public class TurnoDiarioAsignadoSobreDiaConAusenciaTests
    : PrivateEventHandlerAsyncTest<ProgramacionTurnoDiarioSolicitada>
{
    private static readonly Guid AusenciaId = Guid.Parse("019600b0-0000-7000-8000-000000000020");
    private static readonly Guid SolicitudId = Guid.Parse("019600b0-0000-7000-8000-000000000002");
    private static readonly ColaboradorProgramado Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly ResumenColaborador ColaboradorResumen = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    protected override IPrivateEventHandlerAsync<ProgramacionTurnoDiarioSolicitada> Handler =>
        new ProgramacionTurnoDiarioSolicitadaEventHandler(EventStore, PrivateEventSender);

    // CA-4
    [Fact]
    public async Task ProgramacionTurnoDiarioSolicitada_ActualizaElTurnoDeDebajoYMantieneLaAusencia_CuandoElDiaTieneAusencia()
    {
        Given(StreamId, AusenciaDiariaAsignada.Crear(StreamId, Colaborador, Fecha, AusenciaId, "Vacaciones"));

        await WhenAsync(new ProgramacionTurnoDiarioSolicitada(
            SolicitudId, ColaboradorResumen, Fecha,
            new DetalleTurno("Turno Manana",
                [new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")], "")));

        Then(StreamId, new TurnoDiarioAsignado(
            StreamId, Colaborador, Fecha,
            new TurnoDiario("Turno Manana",
                [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "")], ""),
            SolicitudId));
        And<ControlDiarioAggregateRoot, string?>(StreamId, c => c.DetalleTurno!.Nombre, "Turno Manana");
        And<ControlDiarioAggregateRoot, int>(StreamId, c => c.ControlesDeFranja.Count, 0);
        ThenIsPublishedPrivately(new DiaDepurado(
            Colaborador.CodigoColaborador, Fecha, ColaboradorResumen, null, [], [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), "Vacaciones"));
    }
}
