namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

internal static class NotacionDuracion
{
    private const int MinutosPorHora = 60;

    public static string Texto(int minutosTotales) => Texto(minutosTotales / MinutosPorHora, minutosTotales % MinutosPorHora);

    public static string Texto(int horas, int minutos) => (horas, minutos) switch
    {
        (_, 0) => $"{horas} h",
        (0, _) => $"{minutos} min",
        _ => $"{horas} h {minutos} min",
    };
}
