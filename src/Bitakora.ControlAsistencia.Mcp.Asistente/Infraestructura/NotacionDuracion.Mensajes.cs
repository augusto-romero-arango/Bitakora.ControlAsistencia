using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

internal static partial class NotacionDuracion
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura.NotacionDuracionMensajes",
        typeof(NotacionDuracion).Assembly);

    private static class Mensajes
    {
        public static string Horas => ResourceManager.GetString(nameof(Horas))!;

        public static string Minutos => ResourceManager.GetString(nameof(Minutos))!;
    }
}
