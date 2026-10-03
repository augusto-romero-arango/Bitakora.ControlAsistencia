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
        Ausencias = [.. Ausencias, AusenciaVigente.Nueva(e.AusenciaId, e.Colaborador, e.FechaInicio, e.FechaFin, e.Motivo)];
    }

    public void Apply(AusenciaCancelada e) =>
        Ausencias = [.. Ausencias.Select(a => a.Id == e.AusenciaId ? a.SinFechas(e.Fechas) : a)];

    internal ResultadoCancelarAusencia CancelarFechas(Guid ausenciaId, IReadOnlyList<DateOnly> fechas)
    {
        var ausencia = Ausencias.FirstOrDefault(a => a.Id == ausenciaId);
        if (ausencia is null)
            return new ResultadoCancelarAusencia.AusenciaInexistente();

        var vigentes = fechas.Distinct().Where(ausencia.Cubre).Order().ToList();
        if (vigentes.Count == 0)
            return new ResultadoCancelarAusencia.SinCambios();

        var evento = new AusenciaCancelada(ausenciaId, ausencia.Colaborador, vigentes);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return new ResultadoCancelarAusencia.Canceladas(ausencia.Colaborador, vigentes);
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

        var fechasEnConflicto = Enumerable
            .Range(0, fechaFin.DayNumber - fechaInicio.DayNumber + 1)
            .Select(fechaInicio.AddDays)
            .Where(fecha => Ausencias.Any(a => a.Cubre(fecha)))
            .ToList();
        if (fechasEnConflicto.Count > 0)
        {
            var choque = Ausencias.First(a => a.Cubre(fechasEnConflicto[0]));
            return new ResultadoProgramarAusencia.ChocaConAusencia(fechasEnConflicto, choque.Motivo);
        }

        var evento = new AusenciaProgramada(id, colaborador, fechaInicio, fechaFin, motivo);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return new ResultadoProgramarAusencia.Programada();
    }

    internal IReadOnlyList<AusenciaDelColaborador> ListarAusenciasVigentes(DateOnly desde, DateOnly hasta)
        => [.. Ausencias
            .Where(a => a.Fechas.Count > 0 && a.Fechas[0] <= hasta && desde <= a.Fechas[^1])
            .OrderBy(a => a.Fechas[0])
            .Select(a => new AusenciaDelColaborador(
                a.Id, a.Motivo.Nombre, a.Fechas[0], a.Fechas[^1], a.Tramos()))];

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

    internal sealed record AusenciaVigente(
        Guid Id, ColaboradorProgramado Colaborador, IReadOnlyList<DateOnly> Fechas, MotivoAusencia Motivo)
    {
        public static AusenciaVigente Nueva(
            Guid id, ColaboradorProgramado colaborador, DateOnly inicio, DateOnly fin, MotivoAusencia motivo)
            => new(id, colaborador,
                [.. Enumerable.Range(0, fin.DayNumber - inicio.DayNumber + 1).Select(inicio.AddDays)], motivo);

        public bool Cubre(DateOnly fecha) => Fechas.Contains(fecha);

        public AusenciaVigente SinFechas(IReadOnlyList<DateOnly> canceladas) =>
            this with { Fechas = [.. Fechas.Except(canceladas)] };

        public IReadOnlyList<TramoVigente> Tramos()
        {
            var tramos = new List<TramoVigente>();
            foreach (var fecha in Fechas)
            {
                if (tramos.Count > 0 && tramos[^1].Hasta.AddDays(1) == fecha)
                    tramos[^1] = tramos[^1] with { Hasta = fecha };
                else
                    tramos.Add(new TramoVigente(fecha, fecha));
            }
            return tramos;
        }
    }
}
