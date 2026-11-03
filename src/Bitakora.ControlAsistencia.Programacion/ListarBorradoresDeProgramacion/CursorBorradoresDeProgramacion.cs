namespace Bitakora.ControlAsistencia.Programacion.ListarBorradoresDeProgramacion;

public sealed record CursorBorradoresDeProgramacion(string NombreCompleto, string CodigoColaborador)
{
    public string Codificar() => throw new NotImplementedException();

    public static CursorBorradoresDeProgramacion? Decodificar(string texto) => throw new NotImplementedException();
}
