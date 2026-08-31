using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado. El literal del ResourceManager debe seguir
// la ruta logica del .resx dentro del ensamblado; un movimiento de carpeta lo rompe en runtime, no
// en el build (lo cubre AsignarSedeCommandHandlerMensajesTests).
public partial class AsignarSedeCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction.CommandHandler.AsignarSedeCommandHandlerMensajes",
        typeof(AsignarSedeCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string VinculacionTerminada =>
            ResourceManager.GetString(nameof(VinculacionTerminada))!;
    }
}
