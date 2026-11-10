using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.CommandHandler;

public partial class IniciarVinculacionCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.CommandHandler.IniciarVinculacionCommandHandlerMensajes",
        typeof(IniciarVinculacionCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string VinculacionAbierta =>
            ResourceManager.GetString(nameof(VinculacionAbierta))!;

        public static string FechaSolapaVinculacionAnterior =>
            ResourceManager.GetString(nameof(FechaSolapaVinculacionAnterior))!;
    }
}
