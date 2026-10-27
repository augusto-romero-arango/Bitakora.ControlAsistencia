using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction.CommandHandler;

public partial class ModificarVentanaDeEntradaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction.CommandHandler.ModificarVentanaDeEntradaCommandHandlerMensajes",
        typeof(ModificarVentanaDeEntradaCommandHandler).Assembly);

    public static class Mensajes
    {
        public static string TurnoNoEncontrado => ResourceManager.GetString(nameof(TurnoNoEncontrado))!;
        public static string TurnoRetirado => ResourceManager.GetString(nameof(TurnoRetirado))!;
        public static string TurnoSinFranjas => ResourceManager.GetString(nameof(TurnoSinFranjas))!;
        public static string CierrePasaMedianoche => ResourceManager.GetString(nameof(CierrePasaMedianoche))!;

        public static string TurnoDeHorarioAbierto =>
            ResourceManager.GetString(nameof(TurnoDeHorarioAbierto))!;
    }
}
