using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// MEF-ADR-0009: labels de presentacion de CatalogoTurnos en .resx separado
// internal: accesible desde tests via InternalsVisibleTo en el .csproj
public partial class CatalogoTurnos
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.Entities.CatalogoTurnosMensajes",
        typeof(CatalogoTurnos).Assembly);

    internal static class Mensajes
    {
        public static string LabelDescanso =>
            ResourceManager.GetString(nameof(LabelDescanso))!;
    }
}
