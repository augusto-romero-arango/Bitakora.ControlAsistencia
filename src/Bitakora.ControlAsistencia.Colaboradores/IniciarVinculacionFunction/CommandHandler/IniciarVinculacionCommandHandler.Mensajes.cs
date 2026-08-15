using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado. Reemplaza a
// ReingresarColaboradorCommandHandler.Mensajes (issue #350): mismas 3 claves, texto reescrito en
// terminos de iniciar vinculacion donde mencionaba "reingreso" como operacion (CA-4).
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
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
