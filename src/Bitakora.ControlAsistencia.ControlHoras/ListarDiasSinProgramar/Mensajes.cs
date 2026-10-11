using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

public static class Mensajes
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar.Mensajes",
        typeof(Mensajes).Assembly);

    public static string Obtener(string clave, params object[] argumentos) =>
        string.Format(ResourceManager.GetString(clave)!, argumentos);
}
