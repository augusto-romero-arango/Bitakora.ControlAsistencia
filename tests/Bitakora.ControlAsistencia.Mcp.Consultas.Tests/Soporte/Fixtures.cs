namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

public static class Fixtures
{
    public static string Leer(string nombre) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", nombre));
}
