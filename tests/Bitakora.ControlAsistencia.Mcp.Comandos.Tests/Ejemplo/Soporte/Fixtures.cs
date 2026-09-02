namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Ejemplo.Soporte;

public static class Fixtures
{
    public static string Leer(string nombre) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ejemplo", "Fixtures", nombre));
}
