using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ListarFichasTurno;

public sealed record ListaFichasTurnoRespuesta(IReadOnlyList<FichaTurno> Elementos, string? SiguienteCursor);

internal static class PaginaFichasTurno
{
    public static ListaFichasTurnoRespuesta Cortar(IReadOnlyList<FichaTurno> pedidas, int? take)
    {
        if (take is not { } tope || pedidas.Count <= tope)
            return new ListaFichasTurnoRespuesta(pedidas, null);

        var pagina = pedidas.Take(tope).ToList();
        var ultima = pagina[^1];
        return new ListaFichasTurnoRespuesta(pagina, new CursorFichaTurno(ultima.Nombre, ultima.Id).Codificar());
    }
}
