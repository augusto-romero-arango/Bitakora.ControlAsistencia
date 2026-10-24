using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class CatalogoTurnos
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.Entities.CatalogoTurnosMensajes",
        typeof(CatalogoTurnos).Assembly);

    internal static class Mensajes
    {
        public static string DuracionHorarioAbiertoFueraDeRango =>
            ResourceManager.GetString(nameof(DuracionHorarioAbiertoFueraDeRango))!;
    }
}
