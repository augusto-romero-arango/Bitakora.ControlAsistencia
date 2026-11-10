using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;

public partial class AnularTerminacionCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler.AnularTerminacionCommandHandlerMensajes",
        typeof(AnularTerminacionCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string VinculacionAbierta =>
            ResourceManager.GetString(nameof(VinculacionAbierta))!;

        public static string CodigoNoCorresponde =>
            ResourceManager.GetString(nameof(CodigoNoCorresponde))!;
    }
}
