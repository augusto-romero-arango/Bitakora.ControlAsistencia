using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Rendimiento.Plataforma;

public sealed record ConsultaDeMetricas(string Recurso, IReadOnlyList<string> Nombres, string? Filtro);

public sealed record PuntoDeMetrica(DateTimeOffset Minuto, double? Promedio, double? Maximo);

public sealed record SerieDeMetrica(
    string Recurso, string Metrica, string? Entidad, string Unidad, IReadOnlyList<PuntoDeMetrica> Puntos)
{
    public double? MaximoDeLaVentana => Puntos.Max(p => p.Maximo);

    public double? PromedioDeLaVentana
    {
        get
        {
            var valores = Puntos.Where(p => p.Promedio is not null).Select(p => p.Promedio!.Value).ToList();
            return valores.Count == 0 ? null : valores.Average();
        }
    }
}

public sealed record MetricasDeLaVentana(
    DateTimeOffset Desde, DateTimeOffset Hasta, IReadOnlyList<SerieDeMetrica> Series, IReadOnlyList<string> Errores);

// Metricas de plataforma (no se muestrean, a diferencia de App Insights: CA-ADR-0009), minuto a
// minuto, de los recursos de la seccion "Metricas" de appsettings.json. Un recurso que falla no
// tumba a los demas: queda en Errores y el reporte lo muestra.
public static class MetricasDeAzureMonitor
{
    private const string ApiVersion = "2023-10-01";

    public static IReadOnlyList<ConsultaDeMetricas> Configuradas() =>
        Configuracion.Actual.GetSection("Metricas").GetChildren()
            .Select(s => new ConsultaDeMetricas(
                s["Recurso"]!,
                s.GetSection("Nombres").GetChildren().Select(n => n.Value!).ToList(),
                s["Filtro"]))
            .ToList();

    public static async Task<MetricasDeLaVentana> ConsultarAsync(
        ClienteArm arm, IReadOnlyList<ConsultaDeMetricas> consultas, DateTimeOffset desde, DateTimeOffset hasta,
        CancellationToken ct)
    {
        var series = new List<SerieDeMetrica>();
        var errores = new List<string>();
        var ventana = $"{desde.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}/{hasta.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}";

        foreach (var consulta in consultas)
        {
            var parametros = $"&metricnames={Uri.EscapeDataString(string.Join(',', consulta.Nombres))}"
                + $"&timespan={Uri.EscapeDataString(ventana)}&interval=PT1M&aggregation=Average,Maximum"
                + (consulta.Filtro is null ? "" : $"&$filter={Uri.EscapeDataString(consulta.Filtro)}");
            try
            {
                var raiz = await arm.GetAsync(
                    $"{consulta.Recurso}/providers/Microsoft.Insights/metrics", ApiVersion, ct, parametros);
                series.AddRange(LeerSeries(consulta.Recurso, raiz));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errores.Add($"{consulta.Recurso}: {ex.Message}");
            }
        }

        return new MetricasDeLaVentana(desde, hasta, series, errores);
    }

    private static IEnumerable<SerieDeMetrica> LeerSeries(string recurso, JsonElement raiz)
    {
        foreach (var metrica in raiz.GetProperty("value").EnumerateArray())
        {
            var nombre = metrica.GetProperty("name").GetProperty("value").GetString()!;
            var unidad = metrica.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : "";
            foreach (var serie in metrica.GetProperty("timeseries").EnumerateArray())
            {
                string? entidad = null;
                if (serie.TryGetProperty("metadatavalues", out var dimensiones))
                    entidad = string.Join(", ", dimensiones.EnumerateArray()
                        .Select(d => d.GetProperty("value").GetString()));

                var puntos = serie.GetProperty("data").EnumerateArray()
                    .Select(p => new PuntoDeMetrica(
                        DateTimeOffset.Parse(p.GetProperty("timeStamp").GetString()!, CultureInfo.InvariantCulture),
                        Leer(p, "average"),
                        Leer(p, "maximum")))
                    .ToList();

                yield return new SerieDeMetrica(recurso, nombre, string.IsNullOrEmpty(entidad) ? null : entidad, unidad, puntos);
            }
        }
    }

    private static double? Leer(JsonElement punto, string agregacion) =>
        punto.TryGetProperty(agregacion, out var valor) && valor.ValueKind == JsonValueKind.Number
            ? valor.GetDouble()
            : null;
}
