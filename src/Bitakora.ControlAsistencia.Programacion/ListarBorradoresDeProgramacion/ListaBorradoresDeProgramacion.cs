using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarBorradoresDeProgramacion;

public sealed record DiaPendiente(
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<Bloque> Bloques,
    bool EsHorarioAbierto,
    bool Vencido);

public sealed record BorradorDeColaborador(
    string CodigoColaborador,
    string Identificacion,
    string NombreCompleto,
    IReadOnlyList<DiaPendiente> Dias);

public sealed record ListaBorradoresDeProgramacion(
    IReadOnlyList<BorradorDeColaborador> Elementos,
    string? SiguienteCursor = null)
{
    public static ListaBorradoresDeProgramacion Componer(
        DateOnly fechaReferencia, DateOnly? desde, DateOnly? hasta,
        IEnumerable<BorradorDeProgramacion> borradores,
        int? take = null, CursorBorradoresDeProgramacion? cursor = null)
    {
        var colaboradores = borradores
            .Select(b => new BorradorDeColaborador(
                b.CodigoColaborador,
                b.Identificacion,
                b.NombreCompleto,
                b.Dias
                    .Where(d => (desde is not { } ini || d.Fecha >= ini) && (hasta is not { } fin || d.Fecha <= fin))
                    .OrderBy(d => d.Fecha)
                    .Select(d => new DiaPendiente(
                        d.Fecha, d.NombreTurno, d.HorarioResumido, d.Bloques, d.EsHorarioAbierto,
                        d.Fecha < fechaReferencia))
                    .ToList()))
            .Where(c => c.Dias.Count > 0)
            .OrderBy(c => c.NombreCompleto, StringComparer.Ordinal)
            .ThenBy(c => c.CodigoColaborador, StringComparer.Ordinal)
            .Where(c => cursor is not { } k
                || string.CompareOrdinal(c.NombreCompleto, k.NombreCompleto) > 0
                || (c.NombreCompleto == k.NombreCompleto
                    && string.CompareOrdinal(c.CodigoColaborador, k.CodigoColaborador) > 0))
            .ToList();

        if (take is not { } tope || colaboradores.Count <= tope)
            return new ListaBorradoresDeProgramacion(colaboradores);

        var pagina = colaboradores.Take(tope).ToList();
        var ultimo = pagina[^1];
        return new ListaBorradoresDeProgramacion(
            pagina, new CursorBorradoresDeProgramacion(ultimo.NombreCompleto, ultimo.CodigoColaborador).Codificar());
    }
}
