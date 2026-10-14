namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public enum TipoCoincidenciaDePalabra
{
    Exacta,
    Prefijo,
    UnaLetra
}

public sealed record PalabraCoincidente(string Pedida, string Encontrada, TipoCoincidenciaDePalabra Tipo);

public static class CoincidenciaDeTexto
{
    /// <summary>Palabras pedidas que coinciden con alguna palabra del texto; vacio si ninguna.</summary>
    public static IReadOnlyList<PalabraCoincidente> Evaluar(string pedido, string texto) =>
        throw new NotImplementedException();
}
