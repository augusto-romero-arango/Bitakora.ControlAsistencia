using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarFichasTurno;

public sealed record ListaFichasTurnoRespuesta(IReadOnlyList<FichaTurno> Elementos, string? SiguienteCursor);

internal static class PaginaFichasTurno
{
    public static ListaFichasTurnoRespuesta Cortar(IReadOnlyList<FichaTurno> pedidas, int? take) =>
        throw new NotImplementedException();
}
