using System.Diagnostics;
using Azure.Identity;
using Bitakora.ControlAsistencia.Rendimiento.Plataforma;

namespace Bitakora.ControlAsistencia.Rendimiento.Fixtures;

// Credencial del CLI de Azure: el az login del operador en local y el azure/login (OIDC) en el
// workflow. Sin credencial la medicion sigue: la foto y las metricas quedan como "no disponibles"
// en el reporte, porque el SLI se mide con el reloj del cliente y el conteo en Postgres.
public sealed class AzureFixture : IAsyncLifetime
{
    public ClienteArm? Arm { get; private set; }

    public string? MotivoSinArm { get; private set; }

    public HttpClient Publico { get; } = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async ValueTask InitializeAsync()
    {
        var suscripcion = Configuracion.Actual["Azure:SubscriptionId"];
        if (string.IsNullOrWhiteSpace(suscripcion))
            suscripcion = SuscripcionDelCli();

        if (string.IsNullOrWhiteSpace(suscripcion))
        {
            MotivoSinArm = "Azure:SubscriptionId no esta configurada y 'az account show' no respondio (falta az login).";
            return;
        }

        var arm = new ClienteArm(
            new AzureCliCredential(), suscripcion, Configuracion.Leer("Azure:ResourceGroup", "rg-controlasistencias-dev"));
        try
        {
            await arm.ObtenerTokenAsync(CancellationToken.None);
            Arm = arm;
        }
        catch (AuthenticationFailedException ex)
        {
            arm.Dispose();
            MotivoSinArm = $"Sin credencial de Azure (az login): {ex.Message}";
        }
    }

    public Task<FotoDeInfraestructura> FotografiarAsync(CancellationToken ct) =>
        FotografoDeInfraestructura.TomarAsync(Arm, MotivoSinArm, Publico, ct);

    public async Task<MetricasDeLaVentana> MetricasAsync(DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct) =>
        Arm is null
            ? new MetricasDeLaVentana(desde, hasta, [], [$"Metricas no disponibles: {MotivoSinArm}"])
            : await MetricasDeAzureMonitor.ConsultarAsync(Arm, MetricasDeAzureMonitor.Configuradas(), desde, hasta, ct);

    private static string? SuscripcionDelCli()
    {
        try
        {
            using var az = Process.Start(new ProcessStartInfo("az", ["account", "show", "--query", "id", "-o", "tsv"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;
            var salida = az.StandardOutput.ReadToEnd().Trim();
            az.WaitForExit();
            return az.ExitCode == 0 ? salida : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    public ValueTask DisposeAsync()
    {
        Arm?.Dispose();
        Publico.Dispose();
        return ValueTask.CompletedTask;
    }
}
