namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;

public sealed record CursorAusenciaColaborador(DateOnly PrimerDia, Guid Id)
{
    public string Codificar() => throw new NotImplementedException();

    public static CursorAusenciaColaborador? Decodificar(string texto) => throw new NotImplementedException();
}

public sealed record ListaAusenciasColaboradorRespuesta(
    IReadOnlyList<AusenciaDelColaborador> Elementos, string? SiguienteCursor);

public static class PaginaAusenciasColaborador
{
    public static ListaAusenciasColaboradorRespuesta Componer(
        IReadOnlyList<AusenciaDelColaborador> vigentes, int? take, CursorAusenciaColaborador? cursor) =>
        throw new NotImplementedException();
}
