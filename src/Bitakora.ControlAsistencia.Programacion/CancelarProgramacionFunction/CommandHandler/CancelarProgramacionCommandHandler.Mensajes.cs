using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction.CommandHandler;

public partial class CancelarProgramacionCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction.CommandHandler.CancelarProgramacionCommandHandlerMensajes",
        typeof(CancelarProgramacionCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string SolicitudYaExiste =>
            ResourceManager.GetString(nameof(SolicitudYaExiste))!;
    }
}
