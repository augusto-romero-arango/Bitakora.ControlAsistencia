using System.Globalization;
using System.Resources;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction.CommandHandler;

public partial class ProgramarAusenciaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction.CommandHandler.ProgramarAusenciaCommandHandlerMensajes",
        typeof(ProgramarAusenciaCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string AusenciaYaExiste =>
            ResourceManager.GetString(nameof(AusenciaYaExiste))!;

        public static string ChoqueConAusencia(IReadOnlyList<DateOnly> fechas, MotivoAusencia motivo) =>
            string.Format(
                CultureInfo.InvariantCulture,
                ResourceManager.GetString(nameof(ChoqueConAusencia))!,
                string.Join(", ", fechas.Select(f => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
                motivo.Nombre);
    }
}
