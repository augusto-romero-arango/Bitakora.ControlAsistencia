using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class AusenciasColaborador : AggregateRoot
{
    private const string PrefijoStream = "ac";

    internal IReadOnlyList<AusenciaVigente> Ausencias { get; private set; } = [];

    internal static string ComputarStreamId(string codigoColaborador) => $"{PrefijoStream}:{codigoColaborador}";

    public void Apply(AusenciaProgramada e)
    {
        Id = ComputarStreamId(e.Colaborador.CodigoColaborador);
        Ausencias = [.. Ausencias, new AusenciaVigente(e.AusenciaId, e.FechaInicio, e.FechaFin, e.Motivo)];
    }

    internal static AusenciasColaborador Iniciar(AusenciaProgramada evento)
    {
        var ausencias = new AusenciasColaborador();
        ausencias._uncommittedEvents.Add(evento);
        ausencias.Apply(evento);
        return ausencias;
    }

    internal ResultadoProgramarAusencia ProgramarAusencia(
        Guid id, ColaboradorProgramado colaborador, DateOnly fechaInicio, DateOnly fechaFin, MotivoAusencia motivo)
    {
        if (Ausencias.Any(a => a.Id == id))
            return new ResultadoProgramarAusencia.IdDuplicado();

        var choque = Ausencias.FirstOrDefault(a => a.FechaInicio <= fechaFin && fechaInicio <= a.FechaFin);
        if (choque is not null)
        {
            var fechasEnConflicto = Enumerable
                .Range(0, fechaFin.DayNumber - fechaInicio.DayNumber + 1)
                .Select(fechaInicio.AddDays)
                .Where(fecha => Ausencias.Any(a => a.FechaInicio <= fecha && fecha <= a.FechaFin))
                .ToList();
            return new ResultadoProgramarAusencia.ChocaConAusencia(fechasEnConflicto, choque.Motivo);
        }

        var evento = new AusenciaProgramada(id, colaborador, fechaInicio, fechaFin, motivo);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return new ResultadoProgramarAusencia.Programada();
    }

    internal sealed record AusenciaVigente(Guid Id, DateOnly FechaInicio, DateOnly FechaFin, MotivoAusencia Motivo);
}
