using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;
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

    public void Apply(AusenciaCancelada e) => throw new NotImplementedException();

    internal ResultadoCancelarAusencia CancelarFechas(Guid ausenciaId, IReadOnlyList<DateOnly> fechas)
        => throw new NotImplementedException();

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
                .Where(fecha => Ausencias.Any(a => a.Cubre(fecha)))
                .ToList();
            return new ResultadoProgramarAusencia.ChocaConAusencia(fechasEnConflicto, choque.Motivo);
        }

        var evento = new AusenciaProgramada(id, colaborador, fechaInicio, fechaFin, motivo);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return new ResultadoProgramarAusencia.Programada();
    }

    internal IReadOnlyList<AusenciaDelColaborador> ListarAusenciasVigentes(DateOnly desde, DateOnly hasta)
        => [.. Ausencias
            .Where(a => a.FechaInicio <= hasta && desde <= a.FechaFin)
            .OrderBy(a => a.FechaInicio)
            .Select(a => new AusenciaDelColaborador(
                a.Id, a.Motivo.Nombre, a.FechaInicio, a.FechaFin, [new TramoVigente(a.FechaInicio, a.FechaFin)]))];

    internal ClasificacionFechas ClasificarFechas(IReadOnlyList<DateOnly> fechas)
    {
        var libres = new List<DateOnly>();
        var conAusencia = new List<FechaConAusencia>();
        foreach (var fecha in fechas)
        {
            if (Ausencias.FirstOrDefault(a => a.Cubre(fecha)) is { } ausencia)
                conAusencia.Add(new FechaConAusencia(fecha, ausencia.Motivo));
            else
                libres.Add(fecha);
        }
        return new ClasificacionFechas(libres, conAusencia);
    }

    internal sealed record AusenciaVigente(Guid Id, DateOnly FechaInicio, DateOnly FechaFin, MotivoAusencia Motivo)
    {
        public bool Cubre(DateOnly fecha) => FechaInicio <= fecha && fecha <= FechaFin;
    }
}
