using Bitakora.ControlAsistencia.ControlHoras.AsignarAusenciaCuandoAusenciaDiariaProgramada.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarAusenciaCuandoAusenciaDiariaProgramada;

// CA-4 (parte 2) del #747: la ausencia que llega despues de su cancelacion se ignora.
public class AusenciaDiariaProgramadaSobreCancelacionPreviaTests
    : PrivateEventHandlerAsyncTest<AusenciaDiariaProgramada>
{
    private static readonly Guid AusenciaId = Guid.Parse("019600b0-0000-7000-8000-000000000020");
    private static readonly ColaboradorProgramado Colaborador = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly ResumenColaborador ColaboradorResumen = new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";

    protected override IPrivateEventHandlerAsync<AusenciaDiariaProgramada> Handler =>
        new AusenciaDiariaProgramadaEventHandler(EventStore, PrivateEventSender);

    [Fact]
    public async Task AusenciaDiariaProgramada_NoEmiteNada_CuandoLaAusenciaYaFueCancelada()
    {
        Given(StreamId, CancelacionAusenciaDiariaRegistrada.Crear(StreamId, AusenciaId, Fecha));

        await WhenAsync(new AusenciaDiariaProgramada(AusenciaId, ColaboradorResumen, Fecha, "Vacaciones"));

        Then(StreamId);
        And<ControlDiarioAggregateRoot, string>(StreamId, c => c.Id, StreamId);
        ThenIsPublishedPrivately();
    }
}
