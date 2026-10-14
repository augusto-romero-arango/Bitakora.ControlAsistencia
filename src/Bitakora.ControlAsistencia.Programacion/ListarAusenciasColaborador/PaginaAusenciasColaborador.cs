namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;

public sealed record ListaAusenciasColaboradorRespuesta(
    IReadOnlyList<AusenciaDelColaborador> Elementos, string? SiguienteCursor);

public static class PaginaAusenciasColaborador
{
    public static ListaAusenciasColaboradorRespuesta Componer(
        IReadOnlyList<AusenciaDelColaborador> vigentes, int? take, CursorAusenciaColaborador? cursor)
    {
        var ordenadas = vigentes
            .OrderBy(a => a.FechaInicio)
            .ThenBy(a => a.Id)
            .Where(a => cursor is not { } c
                || a.FechaInicio > c.PrimerDia
                || (a.FechaInicio == c.PrimerDia && a.Id.CompareTo(c.Id) > 0))
            .ToList();

        if (take is not { } tope || ordenadas.Count <= tope)
            return new ListaAusenciasColaboradorRespuesta(ordenadas, null);

        var pagina = ordenadas.Take(tope).ToList();
        var ultima = pagina[^1];
        return new ListaAusenciasColaboradorRespuesta(
            pagina, new CursorAusenciaColaborador(ultima.FechaInicio, ultima.Id).Codificar());
    }
}
