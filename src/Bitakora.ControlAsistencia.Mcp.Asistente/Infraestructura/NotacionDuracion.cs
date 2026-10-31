namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// Mismo formato que HorasYMinutos.ToString() del dominio, replicado como texto (MEF-ADR-0047 decision 3).
internal static partial class NotacionDuracion
{
    private const int MinutosPorHora = 60;

    public static string Texto(int minutosTotales) => Texto(minutosTotales / MinutosPorHora, minutosTotales % MinutosPorHora);

    public static string Texto(int horas, int minutos) => (horas, minutos) switch
    {
        (_, 0) => $"{horas} {Mensajes.Horas}",
        (0, _) => $"{minutos} {Mensajes.Minutos}",
        _ => $"{horas} {Mensajes.Horas} {minutos} {Mensajes.Minutos}",
    };
}
