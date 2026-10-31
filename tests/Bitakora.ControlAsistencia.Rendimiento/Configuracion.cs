using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Rendimiento;

// Mismo orden que los SmokeTests: appsettings.json versionado, appsettings.local.json ignorado por
// Git y variables de entorno (las que pone el workflow rendimiento.yml). Una sola raiz para todo el
// ensamblado porque varias fixtures y benchmarks leen las mismas secciones.
public static class Configuracion
{
    private static readonly Lazy<IConfigurationRoot> Raiz = new(() => new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.local.json", optional: true)
        .AddEnvironmentVariables()
        .Build());

    public static IConfigurationRoot Actual => Raiz.Value;

    public static string Obligatoria(string clave, string comoObtenerla)
    {
        var valor = Actual[clave];
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException(
                $"{clave} no esta configurada. {comoObtenerla} Pasala por appsettings.local.json o por la variable de entorno {clave.Replace(":", "__")}.");

        return valor;
    }

    public static T Leer<T>(string clave, T porDefecto) => Actual.GetValue(clave, porDefecto) ?? porDefecto;

    /// <summary>Raiz del repositorio (donde vive el .slnx), para leer archivos versionados con git.</summary>
    public static DirectoryInfo? RaizDelRepositorio()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "ControlAsistencias.slnx")))
                return dir;

        return null;
    }
}
