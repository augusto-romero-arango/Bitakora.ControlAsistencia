using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class BorradorMensual : AggregateRoot
{
    private const string PrefijoStream = "bm";

    private IReadOnlyDictionary<DateOnly, EntradaBorrador> _entradas = new Dictionary<DateOnly, EntradaBorrador>();

    internal IReadOnlyList<DateOnly> FechasEnBorrador => [.. _entradas.Keys.Order()];

    public static string ComputarStreamId(string codigoColaborador, DateOnly fecha)
        => $"{PrefijoStream}:{codigoColaborador}:{fecha:yyyyMM}";

    public void Apply(ProgramacionTurnoAgregadaAlBorrador e)
    {
        Id = ComputarStreamId(e.Colaborador.CodigoColaborador, e.Fechas[0]);
        var entrada = new EntradaBorrador(e.SolicitudId, e.Colaborador, e.DetalleTurno, e.Sede, e.Jornada);
        var entradas = new Dictionary<DateOnly, EntradaBorrador>(_entradas);
        foreach (var fecha in e.Fechas)
            entradas[fecha] = entrada;
        _entradas = entradas;
    }

    public void Apply(ProgramacionPublicada e) =>
        _entradas = _entradas.Where(p => !e.Fechas.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);

    internal ResultadoPublicacion Publicar(DateOnly desde, DateOnly hasta, AusenciasColaborador? ausencias)
    {
        var enRango = _entradas.Keys.Where(f => f >= desde && f <= hasta).Order().ToList();
        var fechas = ausencias?.ClasificarFechas(enRango).Libres ?? enRango;
        if (fechas.Count == 0)
            return new ResultadoPublicacion([]);

        var colaborador = _entradas[fechas[0]].Colaborador;
        var lotes = fechas
            .GroupBy(f => _entradas[f].SolicitudId)
            .Select(g =>
            {
                var entrada = _entradas[g.First()];
                return new LotePublicado(
                    g.Key, entrada.Colaborador, [.. g], entrada.DetalleTurno, entrada.Sede, entrada.Jornada);
            })
            .ToList();

        var evento = new ProgramacionPublicada(colaborador, fechas);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return new ResultadoPublicacion(lotes);
    }

    private sealed record EntradaBorrador(
        Guid SolicitudId,
        ColaboradorProgramado Colaborador,
        TurnoProgramado DetalleTurno,
        SedeProgramada? Sede,
        JornadaProgramada Jornada);
}
