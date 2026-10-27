namespace Bitakora.ControlAsistencia.Programacion.ListarCuadrosDeTurnos;

internal sealed record CursorCuadroDeTurnos(string Nombre, string Id)
{
    public string Codificar() => throw new NotImplementedException();

    public static CursorCuadroDeTurnos? Decodificar(string texto) => throw new NotImplementedException();
}
