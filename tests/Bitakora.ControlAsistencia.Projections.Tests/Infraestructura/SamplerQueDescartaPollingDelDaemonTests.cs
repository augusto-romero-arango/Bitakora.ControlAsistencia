// Dos niveles: ShouldSample_* es unitario sobre el sampler, pero un Drop en el padre no implica
// por si solo que no se instancie la Activity hija (eso lo decide el runtime de ActivityListener);
// CascadaDePolling_* monta ActivitySource reales sobre un TracerProvider real para probar que el
// hijo Npgsql tambien se descarta y que las demas actividades de Marten sobreviven.
//
// En CascadaDePolling_* el delegado es ParentBasedSampler(TraceIdRatioBasedSampler(1.0)), el mismo
// tipo que compone ConfigurarObservabilidad: el hijo sale null porque ParentBasedSampler ve al padre
// sin Recorded y aplica AlwaysOffSampler. Con AlwaysOnSampler como delegado el hijo no se
// descartaria, asi que no sirve para probar la cascada (los unitarios si lo usan).
using System.Diagnostics;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Marten;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

public class SamplerQueDescartaPollingDelDaemonTests
{
    // Spans reales de la fuente Marten, minoritarios frente al polling del daemon: la senal que el
    // filtro NO puede apagar.
    private const string SpanProyeccionLoading = "marten.turnodiarioview.all.page.loading";
    private const string SpanProyeccionGrouping = "marten.turnodiarioview.all.page.grouping";

    private sealed class ColectorDeActividades : BaseProcessor<Activity>
    {
        public List<Activity> Actividades { get; } = [];

        public override void OnEnd(Activity data) => Actividades.Add(data);
    }

    [Fact]
    public void ShouldSample_RetornaDrop_CuandoElNombreEsElPollingDelDaemon()
    {
        var sampler = new SamplerQueDescartaPollingDelDaemon(new AlwaysOnSampler());
        var parametros = new SamplingParameters(
            default, default, SamplerQueDescartaPollingDelDaemon.NombreSpanPollingDaemon, ActivityKind.Internal);

        var resultado = sampler.ShouldSample(in parametros);

        resultado.Decision.Should().Be(SamplingDecision.Drop);
    }

    [Fact]
    public void ShouldSample_DelegaAlSamplerEnvuelto_CuandoElNombreNoEsElPollingDelDaemon()
    {
        var sampler = new SamplerQueDescartaPollingDelDaemon(new AlwaysOnSampler());
        var parametros = new SamplingParameters(
            default, default, SpanProyeccionLoading, ActivityKind.Internal);

        var resultado = sampler.ShouldSample(in parametros);

        resultado.Decision.Should().Be(SamplingDecision.RecordAndSample);
    }

    [Fact]
    public void ShouldSample_DelegaAlSamplerEnvuelto_CuandoElNombreEsDeOtraFuenteComoNpgsql()
    {
        // El sampler solo ve el nombre, no el source: la proteccion del hijo Npgsql depende de que el
        // padre quede en Drop (ver CascadaDePolling_*).
        var sampler = new SamplerQueDescartaPollingDelDaemon(new AlwaysOnSampler());
        var parametros = new SamplingParameters(default, default, "some-sql-command", ActivityKind.Client);

        var resultado = sampler.ShouldSample(in parametros);

        resultado.Decision.Should().Be(SamplingDecision.RecordAndSample);
    }

    private static Sampler CrearDelegadoRealDeterminista(double ratio) =>
        new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio));

    [Fact]
    public void CascadaDePolling_CreaLaActividadDelDaemonSinRecorded_YSuHijaDeNpgsqlNiSeInstancia()
    {
        var sampler = new SamplerQueDescartaPollingDelDaemon(CrearDelegadoRealDeterminista(1.0));
        var colector = new ColectorDeActividades();
        using var fuenteMarten = new ActivitySource(nameof(SamplerQueDescartaPollingDelDaemonTests) + ".Marten");
        using var fuenteNpgsql = new ActivitySource(nameof(SamplerQueDescartaPollingDelDaemonTests) + ".Npgsql");
        using var provider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampler)
            .AddSource(fuenteMarten.Name)
            .AddSource(fuenteNpgsql.Name)
            .AddProcessor(colector)
            .Build();

        using (var actividadDaemon = fuenteMarten.StartActivity(
                   SamplerQueDescartaPollingDelDaemon.NombreSpanPollingDaemon))
        {
            // El span raiz si se instancia (sin padre, el runtime necesita el contexto para
            // propagarlo a los hijos) pero queda Recorded=False y no llega al exporter.
            actividadDaemon.Should().NotBeNull();
            actividadDaemon!.Recorded.Should().BeFalse();

            using var actividadNpgsql = fuenteNpgsql.StartActivity("execute-batch");

            // El hijo hereda el contexto sin Recorded y ParentBasedSampler lo resuelve con
            // AlwaysOffSampler: ActivitySamplingResult.None, ni siquiera se instancia.
            actividadNpgsql.Should().BeNull();
        }

        colector.Actividades.Should().BeEmpty();
    }

    [Fact]
    public void CascadaDePolling_ConservaLasActividadesDeMartenQueNoSonElPollingDelDaemon()
    {
        var sampler = new SamplerQueDescartaPollingDelDaemon(CrearDelegadoRealDeterminista(1.0));
        var colector = new ColectorDeActividades();
        using var fuenteMarten = new ActivitySource(nameof(SamplerQueDescartaPollingDelDaemonTests) + ".MartenReal");
        using var provider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampler)
            .AddSource(fuenteMarten.Name)
            .AddProcessor(colector)
            .Build();

        using (fuenteMarten.StartActivity(SpanProyeccionLoading))
        {
        }

        using (fuenteMarten.StartActivity(SpanProyeccionGrouping))
        {
        }

        colector.Actividades.Select(a => a.OperationName).Should().BeEquivalentTo(
            [SpanProyeccionLoading, SpanProyeccionGrouping]);
    }

    [Fact]
    public void NombreSpanPollingDaemon_CoincideConOtelPrefixDeMartenMasElSufijoDeHighWaterAgent()
    {
        // Oraculo contra la API real de Marten, no contra el literal de la constante: si Marten
        // cambiara OtelPrefix, esta guarda debe fallar (MEF-ADR-0002).
        var otelPrefix = new StoreOptions().Projections.OtelPrefix;

        SamplerQueDescartaPollingDelDaemon.NombreSpanPollingDaemon.Should()
            .Be($"{otelPrefix}.daemon.highwatermark");
    }

    [Fact]
    public void Description_EmbebeLaDelSamplerEnvuelto()
    {
        // El wrapper no publica el delegado (MEF-ADR-0012): lo describe, y asi el guardrail de
        // composicion lee el ratio del sampler efectivo por su API publica.
        var envuelto = new ParentBasedSampler(new TraceIdRatioBasedSampler(0.5));

        var sampler = new SamplerQueDescartaPollingDelDaemon(envuelto);

        sampler.Description.Should()
            .Contain(nameof(SamplerQueDescartaPollingDelDaemon)).And
            .Contain(envuelto.Description);
    }
}
