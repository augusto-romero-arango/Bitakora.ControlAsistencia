namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

public static class Fixtures
{
    public static string Leer(string carpeta, string nombre) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, carpeta, "Fixtures", nombre));
}
