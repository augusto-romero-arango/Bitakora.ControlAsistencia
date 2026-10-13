namespace Bitakora.ControlAsistencia.Sedes.ListarFichasSede;

internal sealed record CursorFichaSede(string Codigo, string Id)
{
    public string Codificar() => throw new NotImplementedException();

    public static CursorFichaSede? Decodificar(string texto) => throw new NotImplementedException();
}
