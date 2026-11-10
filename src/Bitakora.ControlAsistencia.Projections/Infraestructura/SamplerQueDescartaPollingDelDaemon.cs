using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Envuelve al sampler de ratio y descarta el span de polling del daemon HotCold
/// (`marten.daemon.highwatermark`, cada 5 s, sin valor diagnostico), del que cuelga el 95% de los
/// spans Postgres del worker. Drop en la raiz basta: el hijo Npgsql ni se instancia (verificado
/// empiricamente), asi que no hace falta un BaseProcessor.
///
/// No se generaliza a los Function Apps: no corren daemon.
/// </summary>
public sealed class SamplerQueDescartaPollingDelDaemon : Sampler
{
    // HighWaterAgent arma el nombre como `OtelPrefix + ".daemon.highwatermark"`; no existe como
    // literal en JasperFx.Events 2.18.1 / Marten 9.12.0. SamplerQueDescartaPollingDelDaemonTests lo
    // contrasta contra `new StoreOptions().Projections.OtelPrefix` para que un cambio de prefijo no
    // deje este filtro inerte en silencio.
    internal const string NombreSpanPollingDaemon = "marten.daemon.highwatermark";

    private readonly Sampler _delegado;

    public SamplerQueDescartaPollingDelDaemon(Sampler delegado)
    {
        _delegado = delegado;

        // Al estilo de OpenTelemetry ("ParentBased{TraceIdRatioBasedSampler{0.200000}}"): expone el
        // sampler envuelto sin publicar el delegado (MEF-ADR-0012). Los tests leen esta cadena para
        // comprobar que TELEMETRY_SAMPLING_RATIO llega al sampler efectivo.
        Description = $"{nameof(SamplerQueDescartaPollingDelDaemon)}{{{delegado.Description}}}";
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
        samplingParameters.Name == NombreSpanPollingDaemon
            ? new SamplingResult(SamplingDecision.Drop)
            : _delegado.ShouldSample(in samplingParameters);
}
