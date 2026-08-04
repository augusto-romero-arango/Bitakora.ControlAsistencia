// Issue #308: guardrail para el sampler que filtra el span de polling del daemon HotCold de
// Marten (CA-4, CA-5, CA-6). Dos niveles de verificacion, deliberadamente:
//
// a) ShouldSample_* -- unitario, directo sobre el sampler, construyendo SamplingParameters a
//    mano. Rapido y determinista, pero no prueba el efecto de cascada sobre el hijo Npgsql (un
//    Drop en el padre no implica per se que OpenTelemetry se abstenga de instanciar la Activity
//    hija -- eso lo decide el runtime de Activity/ActivityListener, no el sampler).
// b) CascadaDePolling_* -- de integracion, con ActivitySource reales + un colector de actividades
//    en memoria (BaseProcessor<Activity>) montado sobre un TracerProvider real. Reproduce
//    exactamente el wiring que el planner verifico empiricamente: "padre daemon -> Activity
//    creada, Recorded=False; hijo npgsql -> Activity NULL; Exportados: solo el escenario con
//    trabajo real". Sin este nivel, un test unitario en verde no demuestra que el hijo Npgsql
//    tambien se descarta (CA-4) ni que las demas actividades de Marten sobreviven (CA-5).
//
// El delegado en CascadaDePolling_* es ParentBasedSampler(TraceIdRatioBasedSampler(1.0)) --
// el MISMO tipo compuesto que produce ConfigurarObservabilidad, con ratio 1.0 para que sea
// determinista (Notas tecnicas del issue). Es deliberado, no un detalle de implementacion:
// la razon por la que el hijo Npgsql sale NULL (CA-4) es que ParentBasedSampler mira
// parentContext.TraceFlags y, como el padre quedo en Drop (TraceFlags sin Recorded),
// devuelve AlwaysOffSampler para el hijo -- verificado empiricamente reproduciendo este
// wiring exacto en un scratch aislado antes de escribir este archivo. Con AlwaysOnSampler
// como delegado el hijo NO se descartaria (ignora el contexto del padre), asi que ese
// sampler simplificado no sirve para probar la cascada real; los tests unitarios de
// ShouldSample de mas abajo si lo usan, porque ahi no importa el contexto del padre.
using System.Diagnostics;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Marten;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

public class SamplerQueDescartaPollingDelDaemonTests
{
    // Spans reales de la fuente Marten medidos en las 24h del issue #308 (70 y 48 ocurrencias frente
    // a los ~69k del polling del daemon): la senal que el filtro NO puede apagar (CA-5).
    private const string SpanProyeccionLoading = "marten.turnodiarioview.all.page.loading";
    private const string SpanProyeccionGrouping = "marten.turnodiarioview.all.page.grouping";

    // Colector en memoria: hace las veces de "exporter" para poder inspeccionar, sin Application
    // Insights ni infra real, que actividades sobrevivieron el pipeline completo de sampling.
    private sealed class ColectorDeActividades : BaseProcessor<Activity>
    {
        public List<Activity> Actividades { get; } = [];

        public override void OnEnd(Activity data) => Actividades.Add(data);
    }

    // --- ShouldSample directo (CA-4, CA-5) ---

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
        // El sampler NO conoce el source, solo el nombre de la actividad: la proteccion del hijo
        // Npgsql (CA-4) no depende de que este sampler distinga sources, sino de que el padre haya
        // quedado en Drop (ver CascadaDePolling_* abajo, verificacion de efecto real).
        var sampler = new SamplerQueDescartaPollingDelDaemon(new AlwaysOnSampler());
        var parametros = new SamplingParameters(default, default, "some-sql-command", ActivityKind.Client);

        var resultado = sampler.ShouldSample(in parametros);

        resultado.Decision.Should().Be(SamplingDecision.RecordAndSample);
    }

    // --- Cascada real padre (daemon) / hijo (Npgsql) (CA-4, CA-5) ---

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
            // El span raiz SI se instancia (PropagationData: sin padre, el runtime necesita el
            // contexto para poder propagarlo a los hijos) pero queda Recorded=False -- no llega
            // al colector/exporter. Verificado empiricamente reproduciendo este wiring exacto.
            actividadDaemon.Should().NotBeNull();
            actividadDaemon!.Recorded.Should().BeFalse();

            using var actividadNpgsql = fuenteNpgsql.StartActivity("execute-batch");

            // El hijo SI hereda el contexto (TraceId del padre, sin Recorded) y el delegado real
            // (ParentBasedSampler) lo resuelve por ese contexto sin Recorded -> AlwaysOffSampler:
            // ActivitySamplingResult.None -> ni siquiera se instancia.
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

    // --- Guardrail CA-6: el nombre no puede quedar como literal huerfano ---

    [Fact]
    public void NombreSpanPollingDaemon_CoincideConOtelPrefixDeMartenMasElSufijoDeHighWaterAgent()
    {
        // Oraculo independiente (MEF-ADR-0002): se arma contra la API real de Marten
        // (StoreOptions().Projections.OtelPrefix), no contra el mismo literal que la constante ya
        // fija -- de lo contrario esta guarda pasaria siempre, incluso si Marten cambiara
        // OtelPrefix, que es exactamente el modo de falla silenciosa que el issue #308 corrige.
        var otelPrefix = new StoreOptions().Projections.OtelPrefix;

        SamplerQueDescartaPollingDelDaemon.NombreSpanPollingDaemon.Should()
            .Be($"{otelPrefix}.daemon.highwatermark");
    }

    // --- El sampler envuelto queda descrito en la superficie publica (soporte de CA-3) ---

    [Fact]
    public void Description_EmbebeLaDelSamplerEnvuelto()
    {
        // El wrapper no publica el delegado (MEF-ADR-0012, Tell-don't-Ask): lo describe. Asi el
        // guardrail de composicion (ConfiguracionObservabilidadProjectionsTests, CA-3) puede leer el
        // ratio configurado del sampler efectivo por su API publica, sin afirmar estado interno.
        var envuelto = new ParentBasedSampler(new TraceIdRatioBasedSampler(0.5));

        var sampler = new SamplerQueDescartaPollingDelDaemon(envuelto);

        sampler.Description.Should()
            .Contain(nameof(SamplerQueDescartaPollingDelDaemon)).And
            .Contain(envuelto.Description);
    }
}
