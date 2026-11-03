namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed record TramoDeFechas(DateOnly Desde, DateOnly Hasta);

public static class CorteEnTramos
{
    public static IReadOnlyList<TramoDeFechas> Cortar(DateOnly desde, DateOnly hasta, int maximoDias) =>
        throw new NotImplementedException();
}
