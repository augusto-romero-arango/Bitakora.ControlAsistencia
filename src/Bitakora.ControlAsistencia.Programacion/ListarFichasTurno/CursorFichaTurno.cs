namespace Bitakora.ControlAsistencia.Programacion.ListarFichasTurno;

internal sealed record CursorFichaTurno(string Nombre, string Id)
{
    public string Codificar() => throw new NotImplementedException();

    public static CursorFichaTurno? Decodificar(string texto) => throw new NotImplementedException();
}
