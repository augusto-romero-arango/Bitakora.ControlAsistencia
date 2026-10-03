using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

public sealed record TramoAplicado(DateOnly Desde, DateOnly Hasta);

public sealed record AusenciaDelPeriodo(Guid Id, string Motivo, IReadOnlyList<TramoAplicado> Tramos);

public sealed record AusenciasDeColaborador(
    string CodigoColaborador,
    string NombreCompleto,
    IReadOnlyList<AusenciaDelPeriodo> Ausencias);

public sealed record ListaAusenciasDelEquipo(
    DateOnly Desde,
    DateOnly Hasta,
    bool RangoRecortado,
    IReadOnlyList<AusenciasDeColaborador> Colaboradores)
{
    // Los tramos se recortan al periodo aplicado; el calendario por dia lo arma el cliente.
    public static ListaAusenciasDelEquipo Componer(
        DateOnly desde, RangoAplicado rango, IEnumerable<AusenciaVigente> vigentes)
    {
        var hasta = rango.HastaAplicado;

        var colaboradores = vigentes
            .Select(a => (Vista: a, Tramos: Recortar(a.TramosVigentes, desde, hasta)))
            .Where(x => x.Tramos.Count > 0)
            .GroupBy(x => x.Vista.CodigoColaborador)
            .Select(g => new AusenciasDeColaborador(
                g.Key,
                g.First().Vista.NombreCompleto,
                g.OrderBy(x => x.Tramos[0].Desde)
                    .Select(x => new AusenciaDelPeriodo(x.Vista.Id, x.Vista.Motivo, x.Tramos))
                    .ToList()))
            .OrderBy(c => c.NombreCompleto, StringComparer.Ordinal)
            .ThenBy(c => c.CodigoColaborador, StringComparer.Ordinal)
            .ToList();

        return new ListaAusenciasDelEquipo(desde, hasta, rango.RangoRecortado, colaboradores);
    }

    private static List<TramoAplicado> Recortar(IReadOnlyList<TramoDeAusencia> tramos, DateOnly desde, DateOnly hasta) =>
        tramos
            .Where(t => t.Desde <= hasta && t.Hasta >= desde)
            .Select(t => new TramoAplicado(t.Desde > desde ? t.Desde : desde, t.Hasta < hasta ? t.Hasta : hasta))
            .ToList();
}
