namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed record TramoDeFechas(DateOnly Desde, DateOnly Hasta);

public static class CorteEnTramos
{
    public static IReadOnlyList<TramoDeFechas> Cortar(DateOnly desde, DateOnly hasta, int maximoDias)
    {
        var tramos = new List<TramoDeFechas>();
        for (var inicio = desde; inicio <= hasta; inicio = inicio.AddDays(maximoDias))
        {
            var restantes = hasta.DayNumber - inicio.DayNumber + 1;
            tramos.Add(new TramoDeFechas(inicio, inicio.AddDays(Math.Min(maximoDias, restantes) - 1)));
        }

        return tramos;
    }
}
