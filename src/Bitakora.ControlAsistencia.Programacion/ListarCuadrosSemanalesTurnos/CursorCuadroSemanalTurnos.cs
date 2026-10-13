namespace Bitakora.ControlAsistencia.Programacion.ListarCuadrosSemanalesTurnos;

internal sealed record CursorCuadroSemanalTurnos(string Nombre, string Id)
{
    public string Codificar() => throw new NotImplementedException();

    public static CursorCuadroSemanalTurnos? Decodificar(string texto) => throw new NotImplementedException();
}
