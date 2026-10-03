using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class AusenciasColaborador : AggregateRoot
{
    internal IReadOnlyList<AusenciaVigente> Ausencias { get; private set; } = [];

    public void Apply(AusenciaProgramada e) => throw new NotImplementedException();

    internal static AusenciasColaborador Iniciar(AusenciaProgramada evento)
        => throw new NotImplementedException();

    internal ResultadoProgramarAusencia ProgramarAusencia(
        Guid id, ColaboradorProgramado colaborador, DateOnly fechaInicio, DateOnly fechaFin, MotivoAusencia motivo)
        => throw new NotImplementedException();

    internal sealed record AusenciaVigente(Guid Id, DateOnly FechaInicio, DateOnly FechaFin, MotivoAusencia Motivo);
}
