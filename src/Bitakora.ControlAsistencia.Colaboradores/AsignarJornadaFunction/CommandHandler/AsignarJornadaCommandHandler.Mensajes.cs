using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction.CommandHandler;

public partial class AsignarJornadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction.CommandHandler.AsignarJornadaCommandHandlerMensajes",
        typeof(AsignarJornadaCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado => ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;
        public static string VinculacionTerminada => ResourceManager.GetString(nameof(VinculacionTerminada))!;
    }
}
