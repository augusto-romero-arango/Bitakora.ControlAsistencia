using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CommandHandler;

public partial class CrearTurnoDeHorarioAbiertoCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CommandHandler.CrearTurnoDeHorarioAbiertoCommandHandlerMensajes",
        typeof(CrearTurnoDeHorarioAbiertoCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string TurnoYaExiste =>
            ResourceManager.GetString(nameof(TurnoYaExiste))!;

        public static string NombreDuplicado =>
            ResourceManager.GetString(nameof(NombreDuplicado))!;
    }
}
