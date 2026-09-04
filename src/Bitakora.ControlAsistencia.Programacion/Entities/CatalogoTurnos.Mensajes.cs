using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// MEF-ADR-0009: el nombre logico del recurso debe coincidir con el .resx co-localizado.
public partial class CatalogoTurnos
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.Entities.CatalogoTurnosMensajes",
        typeof(CatalogoTurnos).Assembly);

    internal static class Mensajes
    {
        public static string LabelDescanso =>
            ResourceManager.GetString(nameof(LabelDescanso))!;

        public static string LabelIncompleto =>
            ResourceManager.GetString(nameof(LabelIncompleto))!;
    }
}
