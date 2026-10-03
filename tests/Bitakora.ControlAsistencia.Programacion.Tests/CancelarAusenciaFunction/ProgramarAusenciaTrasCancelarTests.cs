using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction;
using Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarAusenciaFunction;

public class ProgramarAusenciaTrasCancelarTests : CommandHandlerAsyncTest<ProgramarAusencia>
{
    private const string StreamId = "ac:E001";

    private static readonly Guid PrevioId = Guid.Parse("019600a0-0000-7000-8000-000000000744");
    private static readonly Guid NuevaId = Guid.Parse("019600a0-0000-7000-8000-000000000746");
    private static readonly ColaboradorProgramado Colaborador = new("CC-12345678", "E001", "Ana Maria Gomez");
    private static readonly ResumenColaborador Resumen = new("CC-12345678", "E001", "Ana Maria Gomez");

    protected override ICommandHandlerAsync<ProgramarAusencia> Handler =>
        new ProgramarAusenciaCommandHandler(EventStore, PrivateEventSender);

    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    // CA-6
    [Fact]
    public async Task ProgramarAusencia_AceptaElRango_CuandoLasFechasFueronCanceladas()
    {
        Given(StreamId,
            new AusenciaProgramada(PrevioId, Colaborador, Oct(13), Oct(26), MotivoAusencia.IncapacidadMedica),
            new AusenciaCancelada(PrevioId, Colaborador, [Oct(20), Oct(21), Oct(22)]));

        await WhenAsync(new ProgramarAusencia(
            NuevaId, "E001", "CC-12345678", "Ana Maria Gomez", Oct(20), Oct(22), "Vacaciones"));

        Then(StreamId, new AusenciaProgramada(NuevaId, Colaborador, Oct(20), Oct(22), MotivoAusencia.Vacaciones));
        ThenIsPublishedPrivately(
            new AusenciaDiariaProgramada(NuevaId, Resumen, Oct(20), "Vacaciones"),
            new AusenciaDiariaProgramada(NuevaId, Resumen, Oct(21), "Vacaciones"),
            new AusenciaDiariaProgramada(NuevaId, Resumen, Oct(22), "Vacaciones"));
        And<AusenciasColaborador, int>(StreamId, a => a.Ausencias.Count, 2);
    }
}
