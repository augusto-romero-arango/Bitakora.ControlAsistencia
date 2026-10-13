namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

public sealed record CursorAusenciasEquipo(string NombreCompleto, string CodigoColaborador)
{
    public string Codificar() => throw new NotImplementedException();

    public static CursorAusenciasEquipo? Decodificar(string texto) => throw new NotImplementedException();
}
