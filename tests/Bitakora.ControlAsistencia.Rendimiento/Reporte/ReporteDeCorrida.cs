using System.Globalization;
using System.Text;
using System.Text.Json;
using Bitakora.ControlAsistencia.Rendimiento.Plataforma;

namespace Bitakora.ControlAsistencia.Rendimiento.Reporte;

public sealed record IndicadorDeNivel(string Nombre, double ValorSegundos, double SloSegundos, bool Cumple);

/// <summary>Una muestra del progreso; Detalle lleva columnas extra tomadas en el mismo instante.</summary>
public sealed record MuestraDeProgreso(double Segundos, long Valor, IReadOnlyDictionary<string, long>? Detalle = null);

/// <summary>Cuando ocurrio un eslabon de la cadena, en segundos desde el inicio del SLI.</summary>
public sealed record FaseDeLaCorrida(
    string Nombre, long Cantidad, double PrimeroSegundos, double MedianaSegundos, double P90Segundos, double UltimoSegundos);

/// <summary>
/// Evidencia de una corrida: el SLI contra su SLO, los parametros, los resultados, la foto de la
/// infraestructura y las metricas de la ventana. Se escribe antes de la asercion del SLO para que
/// una corrida roja tambien deje su evidencia.
/// </summary>
public sealed class ReporteDeCorrida(string benchmark, string corrida)
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string Benchmark => benchmark;
    public string Corrida => corrida;
    public DateTimeOffset Inicio { get; } = DateTimeOffset.UtcNow;
    public List<KeyValuePair<string, string>> Parametros { get; } = [];
    public List<KeyValuePair<string, string>> Resultados { get; } = [];
    public List<string> Hallazgos { get; } = [];
    public List<MuestraDeProgreso> Progreso { get; } = [];
    public string EtiquetaDelProgreso { get; set; } = "Valor";
    public List<FaseDeLaCorrida> Fases { get; } = [];
    public IndicadorDeNivel? Sli { get; set; }
    public FotoDeInfraestructura? FotoInicial { get; set; }
    public FotoDeInfraestructura? FotoFinal { get; set; }
    public MetricasDeLaVentana? Metricas { get; set; }
    public object? Detalle { get; set; }

    public void Parametro(string nombre, object valor) => Parametros.Add(new(nombre, Formatear(valor)));

    public void Resultado(string nombre, object valor) => Resultados.Add(new(nombre, Formatear(valor)));

    public async Task<string> EscribirAsync(CancellationToken ct)
    {
        var directorio = Configuracion.Actual["Rendimiento:DirectorioReportes"];
        if (string.IsNullOrWhiteSpace(directorio))
            directorio = Path.Combine(AppContext.BaseDirectory, "reportes");
        Directory.CreateDirectory(directorio);

        var markdown = Markdown();
        var baseDelArchivo = Path.Combine(directorio, $"{benchmark}-{corrida}");
        await File.WriteAllTextAsync($"{baseDelArchivo}.md", markdown, ct);
        await File.WriteAllTextAsync($"{baseDelArchivo}.json", JsonSerializer.Serialize(Documento(), OpcionesJson), ct);

        if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } resumen)
            await File.AppendAllTextAsync(resumen, markdown + Environment.NewLine, ct);

        TestContext.Current.TestOutputHelper?.WriteLine(markdown);
        Console.WriteLine($"Reporte de {benchmark}: {baseDelArchivo}.md");
        return baseDelArchivo;
    }

    private object Documento() => new
    {
        benchmark,
        corrida,
        inicio = Inicio,
        sli = Sli,
        parametros = Parametros.ToDictionary(p => p.Key, p => p.Value),
        resultados = Resultados.ToDictionary(r => r.Key, r => r.Value),
        hallazgos = Hallazgos,
        progreso = new { etiqueta = EtiquetaDelProgreso, muestras = Progreso },
        fases = Fases,
        infraestructura = new
        {
            huellaInicial = FotoInicial?.Huella,
            huellaFinal = FotoFinal?.Huella,
            diferencias = FotoInicial is not null && FotoFinal is not null ? FotoInicial.Diferencias(FotoFinal) : null,
            inicial = FotoInicial,
            erroresFinal = FotoFinal?.Errores
        },
        metricas = Metricas,
        detalle = Detalle
    };

    private string Markdown()
    {
        var md = new StringBuilder();
        md.AppendLine($"# Rendimiento: {benchmark} (corrida {corrida})");
        md.AppendLine();

        if (Sli is { } sli)
        {
            md.AppendLine("| SLI | Valor | SLO | Resultado |");
            md.AppendLine("|---|---|---|---|");
            md.AppendLine($"| {sli.Nombre} | {Segundos(sli.ValorSegundos)} | {Segundos(sli.SloSegundos)} | {(sli.Cumple ? "Cumple" : "**No cumple**")} |");
            md.AppendLine();
        }

        Tabla(md, "Parametros", Parametros);
        Tabla(md, "Resultados", Resultados);

        if (Hallazgos.Count > 0)
        {
            md.AppendLine("## Hallazgos");
            md.AppendLine();
            foreach (var hallazgo in Hallazgos)
                md.AppendLine($"- {hallazgo}");
            md.AppendLine();
        }

        if (Progreso.Count > 0)
        {
            var columnas = Progreso.Where(m => m.Detalle is not null).SelectMany(m => m.Detalle!.Keys)
                .Distinct().Order(StringComparer.Ordinal).ToList();
            md.AppendLine($"## Progreso ({EtiquetaDelProgreso})");
            md.AppendLine();
            md.AppendLine($"| Segundo | {EtiquetaDelProgreso} |{string.Concat(columnas.Select(c => $" {c} |"))}");
            md.AppendLine($"|---|---|{string.Concat(columnas.Select(_ => "---|"))}");
            foreach (var muestra in Submuestrear(Progreso, 30))
                md.AppendLine($"| {muestra.Segundos.ToString("0", CultureInfo.InvariantCulture)} | {muestra.Valor} |"
                    + string.Concat(columnas.Select(c => $" {(muestra.Detalle?.TryGetValue(c, out var v) == true ? v : 0)} |")));
            md.AppendLine();
        }

        if (Fases.Count > 0)
        {
            md.AppendLine("## Fases");
            md.AppendLine();
            md.AppendLine("Segundos desde el inicio del SLI, con los timestamps que pone la base a cada evento y documento.");
            md.AppendLine();
            md.AppendLine("| Fase | Cantidad | Primero | Mediana | p90 | Ultimo |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var fase in Fases)
                md.AppendLine($"| {fase.Nombre} | {fase.Cantidad} | {Segundos(fase.PrimeroSegundos)} | {Segundos(fase.MedianaSegundos)} | "
                    + $"{Segundos(fase.P90Segundos)} | {Segundos(fase.UltimoSegundos)} |");
            md.AppendLine();
        }

        Infraestructura(md);
        SeccionDeMetricas(md);
        return md.ToString();
    }

    private void Infraestructura(StringBuilder md)
    {
        md.AppendLine("## Infraestructura de la corrida");
        md.AppendLine();
        if (FotoInicial is null)
        {
            md.AppendLine("Sin foto de infraestructura.");
            md.AppendLine();
            return;
        }

        md.AppendLine($"Huella inicial `{FotoInicial.Huella}`, final `{FotoFinal?.Huella ?? "sin foto"}`.");
        if (FotoFinal is not null && FotoInicial.Diferencias(FotoFinal) is { Count: > 0 } diferencias)
            md.AppendLine($"**La infraestructura cambio durante la corrida**: {string.Join(", ", diferencias)}.");
        md.AppendLine();

        md.AppendLine($"<details><summary>Configuracion capturada ({FotoInicial.Valores.Count} valores)</summary>");
        md.AppendLine();
        md.AppendLine("| Recurso | Campo | Valor |");
        md.AppendLine("|---|---|---|");
        foreach (var (clave, valor) in FotoInicial.Valores)
        {
            var separador = clave.IndexOf('#');
            var recurso = clave[..separador].Replace("Microsoft.", "", StringComparison.Ordinal);
            var campo = clave[(separador + 1)..];
            md.AppendLine($"| {recurso} | {campo} | `{ResumirValor(campo, valor)}` |");
        }
        md.AppendLine();
        md.AppendLine("</details>");
        md.AppendLine();

        foreach (var error in FotoInicial.Errores)
            md.AppendLine($"- Sin capturar: {error}");
        if (FotoInicial.Errores.Count > 0)
            md.AppendLine();
    }

    private void SeccionDeMetricas(StringBuilder md)
    {
        md.AppendLine("## Metricas de la ventana");
        md.AppendLine();
        if (Metricas is null)
        {
            md.AppendLine("Sin metricas.");
            return;
        }

        md.AppendLine($"Ventana {Metricas.Desde.UtcDateTime:HH:mm}-{Metricas.Hasta.UtcDateTime:HH:mm} UTC, granularidad 1 min.");
        md.AppendLine();
        var conDatos = Metricas.Series.Where(s => s.TieneDatos).ToList();
        if (conDatos.Count > 0)
        {
            md.AppendLine("Por minuto: el maximo en los gauges (CPU, memoria, mensajes activos) y el total en los contadores (mensajes entrantes y completados).");
            md.AppendLine();
            md.AppendLine("| Recurso | Metrica | Entidad | Prom. | Max. | Total | Por minuto |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var serie in conDatos)
                md.AppendLine($"| {serie.Recurso.Split('/')[^1]} | {serie.Metrica} | {serie.Entidad} | "
                    + $"{Numero(serie.PromedioDeLaVentana)} | {Numero(serie.MaximoDeLaVentana)} | {Numero(serie.TotalDeLaVentana)} | "
                    + $"{string.Join(" ", serie.Puntos.Select(p => Numero(p.Valor)))} |");
            md.AppendLine();
        }

        var enCero = Metricas.Series.Count - conDatos.Count;
        if (enCero > 0)
            md.AppendLine($"{enCero} series sin valores mayores que cero no se muestran (estan en el JSON).");
        foreach (var error in Metricas.Errores)
            md.AppendLine($"- Sin metricas: {error}");
        md.AppendLine();
    }

    // El host.json completo vive en el JSON; en el markdown basta lo que cambia el throughput.
    private static string ResumirValor(string campo, string valor)
    {
        if (campo != "host.json")
            return valor.Replace("|", "\\|", StringComparison.Ordinal);

        using var documento = JsonDocument.Parse(valor);
        return documento.RootElement.TryGetProperty("extensions", out var extensiones)
               && extensiones.TryGetProperty("serviceBus", out var serviceBus)
            ? $"serviceBus: {serviceBus.GetRawText()}"
            : "(sin extensions.serviceBus)";
    }

    private static void Tabla(StringBuilder md, string titulo, List<KeyValuePair<string, string>> filas)
    {
        if (filas.Count == 0)
            return;

        md.AppendLine($"## {titulo}");
        md.AppendLine();
        md.AppendLine("| | |");
        md.AppendLine("|---|---|");
        foreach (var (nombre, valor) in filas)
            md.AppendLine($"| {nombre} | {valor} |");
        md.AppendLine();
    }

    private static IEnumerable<MuestraDeProgreso> Submuestrear(List<MuestraDeProgreso> muestras, int maximo)
    {
        if (muestras.Count <= maximo)
            return muestras;

        var paso = (double)(muestras.Count - 1) / (maximo - 1);
        return Enumerable.Range(0, maximo).Select(i => muestras[(int)Math.Round(i * paso)]);
    }

    private static string Formatear(object valor) => valor switch
    {
        TimeSpan duracion => Segundos(duracion.TotalSeconds),
        double numero => Numero(numero),
        IFormattable formateable => formateable.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString() ?? ""
    };

    private static string Segundos(double segundos) => $"{segundos.ToString("0.0", CultureInfo.InvariantCulture)} s";

    private static string Numero(double? valor) =>
        valor is null ? "-" : valor.Value.ToString(valor.Value >= 100 ? "0" : "0.#", CultureInfo.InvariantCulture);
}
