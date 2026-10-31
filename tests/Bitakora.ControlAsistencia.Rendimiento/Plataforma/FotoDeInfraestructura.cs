using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Rendimiento.Plataforma;

/// <summary>
/// Configuracion de la infraestructura sobre la que corrio un benchmark, como pares
/// "{recurso}#{campo}" -> valor. La huella agrupa corridas con la misma infraestructura; las
/// diferencias entre dos fotos aislan el cambio cuyo efecto se quiere medir.
/// </summary>
public sealed record FotoDeInfraestructura(
    DateTimeOffset TomadaEn, IReadOnlyDictionary<string, string> Valores, IReadOnlyList<string> Errores)
{
    public string Huella { get; } = CalcularHuella(Valores);

    public IReadOnlyList<string> Diferencias(FotoDeInfraestructura otra) =>
        Valores.Keys.Union(otra.Valores.Keys)
            .Where(clave => Valores.GetValueOrDefault(clave) != otra.Valores.GetValueOrDefault(clave))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string CalcularHuella(IReadOnlyDictionary<string, string> valores)
    {
        var canonico = string.Join('\n', valores.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key}={v.Value}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)))[..12];
    }
}

// Lee solo lo que declara la seccion "Infraestructura" de appsettings.json: campos de recursos ARM,
// app settings de una allowlist (nunca la lista completa: trae connection strings y el reporte
// termina en un artifact), el SHA desplegado de cada Function App y su host.json en ese commit.
// El host.json se lee de git porque Kudu no lo expone con run-from-package.
public static class FotografoDeInfraestructura
{
    private const string ApiVersionSitios = "2023-12-01";

    public static async Task<FotoDeInfraestructura> TomarAsync(
        ClienteArm? arm, string? motivoSinArm, HttpClient publico, CancellationToken ct)
    {
        var valores = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var errores = new List<string>();
        var seccion = Configuracion.Actual.GetSection("Infraestructura");

        if (arm is null)
            errores.Add($"ARM no disponible: {motivoSinArm}");
        else
            await CapturarRecursosAsync(arm, seccion.GetSection("Recursos"), valores, errores, ct);

        var prefijos = Lista(seccion.GetSection("AppSettingsPermitidos:Prefijos"));
        var exactos = Lista(seccion.GetSection("AppSettingsPermitidos:Exactos"));
        foreach (var app in seccion.GetSection("FunctionApps").GetChildren())
        {
            var nombre = app["Nombre"]!;
            var recurso = $"Microsoft.Web/sites/{nombre}";

            var sha = await LeerShaDesplegadoAsync(publico, nombre, errores, ct);
            if (sha is not null)
            {
                valores[$"{recurso}#sha-desplegado"] = sha;
                if (LeerDeGit(sha, app["HostJson"]!, errores) is { } hostJson)
                    valores[$"{recurso}#host.json"] = hostJson;
            }

            if (arm is null)
                continue;

            try
            {
                var settings = await arm.PostAsync($"{recurso}/config/appsettings/list", ApiVersionSitios, ct);
                foreach (var setting in settings.GetProperty("properties").EnumerateObject())
                    if (exactos.Contains(setting.Name) || prefijos.Any(p => setting.Name.StartsWith(p, StringComparison.Ordinal)))
                        valores[$"{recurso}#appsetting:{setting.Name}"] = setting.Value.ToString();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errores.Add($"{recurso} (app settings): {ex.Message}");
            }
        }

        return new FotoDeInfraestructura(DateTimeOffset.UtcNow, valores, errores);
    }

    private static async Task CapturarRecursosAsync(
        ClienteArm arm, IConfigurationSection recursos, SortedDictionary<string, string> valores, List<string> errores,
        CancellationToken ct)
    {
        foreach (var recurso in recursos.GetChildren())
        {
            var ruta = recurso["Ruta"]!;
            try
            {
                var raiz = await arm.GetAsync(ruta, recurso["ApiVersion"]!, ct);
                foreach (var campo in Lista(recurso.GetSection("Campos")))
                    valores[$"{ruta}#{campo}"] = Extraer(raiz, campo) ?? "(ausente)";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errores.Add($"{ruta}: {ex.Message}");
            }
        }
    }

    /// <summary>Navega rutas tipo "properties.template.containers[0].image".</summary>
    private static string? Extraer(JsonElement raiz, string campo)
    {
        var actual = raiz;
        foreach (var segmento in campo.Split('.'))
        {
            var corchete = segmento.IndexOf('[');
            var nombre = corchete < 0 ? segmento : segmento[..corchete];
            if (actual.ValueKind != JsonValueKind.Object || !actual.TryGetProperty(nombre, out actual))
                return null;

            if (corchete >= 0)
            {
                var indice = int.Parse(segmento[(corchete + 1)..^1]);
                if (actual.ValueKind != JsonValueKind.Array || actual.GetArrayLength() <= indice)
                    return null;
                actual = actual[indice];
            }
        }

        return actual.ValueKind == JsonValueKind.String ? actual.GetString() : actual.GetRawText();
    }

    private static async Task<string?> LeerShaDesplegadoAsync(
        HttpClient publico, string app, List<string> errores, CancellationToken ct)
    {
        // Los dominios responden el SHA en texto plano; el MCP, como {"sha": "..."}.
        try
        {
            var cuerpo = (await publico.GetStringAsync($"https://{app}.azurewebsites.net/api/version", ct)).Trim();
            if (!cuerpo.StartsWith('{'))
                return cuerpo.Trim('"');

            using var documento = JsonDocument.Parse(cuerpo);
            return documento.RootElement.GetProperty("sha").GetString();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errores.Add($"{app} (/api/version): {ex.Message}");
            return null;
        }
    }

    private static string? LeerDeGit(string sha, string ruta, List<string> errores)
    {
        var repositorio = Configuracion.RaizDelRepositorio();
        if (repositorio is null)
        {
            errores.Add($"{ruta}: no se encontro el repositorio para leer el host.json del commit {sha}.");
            return null;
        }

        using var git = Process.Start(new ProcessStartInfo("git", ["-C", repositorio.FullName, "show", $"{sha}:{ruta}"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        var contenido = git.StandardOutput.ReadToEnd();
        var error = git.StandardError.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
        {
            errores.Add($"{ruta}@{sha[..Math.Min(7, sha.Length)]}: {error.Trim()} (en CI hace falta checkout con fetch-depth: 0)");
            return null;
        }

        using var documento = JsonDocument.Parse(contenido, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        return JsonSerializer.Serialize(documento.RootElement);
    }

    private static List<string> Lista(IConfigurationSection seccion) =>
        seccion.GetChildren().Select(c => c.Value!).ToList();
}
