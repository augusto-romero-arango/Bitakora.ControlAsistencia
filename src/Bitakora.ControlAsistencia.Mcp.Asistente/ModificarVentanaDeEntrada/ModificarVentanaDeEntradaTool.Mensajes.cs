using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ModificarVentanaDeEntrada;

public partial class ModificarVentanaDeEntradaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ModificarVentanaDeEntrada.ModificarVentanaDeEntradaToolMensajes",
        typeof(ModificarVentanaDeEntradaTool).Assembly);

    internal static class Mensajes
    {
        public static string ResultadoVentanaModificada =>
            ResourceManager.GetString(nameof(ResultadoVentanaModificada))!;

        public static string EntraEntre => ResourceManager.GetString(nameof(EntraEntre))!;

        public static string TurnoFijo => ResourceManager.GetString(nameof(TurnoFijo))!;

        public static string VentanaDeAmplitud => ResourceManager.GetString(nameof(VentanaDeAmplitud))!;

        public static string Horas => ResourceManager.GetString(nameof(Horas))!;

        public static string Minutos => ResourceManager.GetString(nameof(Minutos))!;

        public static string CampoObligatorio => ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string TurnoIdInvalido => ResourceManager.GetString(nameof(TurnoIdInvalido))!;

        public static string TurnoNoExiste => ResourceManager.GetString(nameof(TurnoNoExiste))!;

        public static string AmplitudInvalida => ResourceManager.GetString(nameof(AmplitudInvalida))!;

        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string NotaVisibilidadEventual => ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
