using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Issue #308 (hallazgo 2): el 95% de los spans Postgres del worker cuelgan del span raiz del
/// daemon HotCold de Marten (`marten.daemon.highwatermark`, un ciclo cada 5s sin valor
/// diagnostico). Este sampler envuelve al sampler de ratio (CA-ADR-0009 Capa 2,
/// ConfiguracionObservabilidadProjections.ConfigurarObservabilidad) y descarta esa actividad SIN
/// delegar -- Drop en la raiz basta para que el hijo Npgsql ni siquiera se instancie (verificado
/// empiricamente por el planner: "hijo npgsql -> Activity NULL"), asi que no hace falta un
/// BaseProcessor ni filtrar el hijo por separado.
///
/// Solo un consumidor (el worker de proyecciones, que corre el daemon) -- MEF-ADR-0018 Rule of
/// Three: NO se generaliza a los Function Apps (ControlHoras/Programacion), que no corren daemon
/// y por tanto no emiten este span.
/// </summary>
public sealed class SamplerQueDescartaPollingDelDaemon : Sampler
{
    // Issue #308, nota tecnica: JasperFx.Events.Daemon.HighWater.HighWaterAgent construye el
    // nombre del span como `_settings.OtelPrefix + ".daemon.highwatermark"`. No existe como
    // literal en ningun ensamblado del marco (JasperFx.Events 2.18.1 / Marten 9.12.0) -- el
    // guardrail CA-6 (SamplerQueDescartaPollingDelDaemonTests) contrasta esta constante contra
    // `new StoreOptions().Projections.OtelPrefix` de la API real para que un cambio futuro de
    // OtelPrefix no deje este filtro inerte en silencio, el mismo modo de falla que este issue
    // esta corrigiendo.
    internal const string NombreSpanPollingDaemon = "marten.daemon.highwatermark";

    // Visible en tests via InternalsVisibleTo (Projections.csproj). Permite verificar CA-3
    // (TELEMETRY_SAMPLING_RATIO llega al sampler efectivo) inspeccionando directamente el sampler
    // envuelto, sin necesitar reflection adicional mas alla del unico paso ya inevitable: leer la
    // propiedad interna `Sampler` de TracerProviderSdk (OpenTelemetry no la expone publicamente).
    internal Sampler Delegado { get; }

    public SamplerQueDescartaPollingDelDaemon(Sampler delegado)
    {
        Delegado = delegado;
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
        samplingParameters.Name == NombreSpanPollingDaemon
            ? new SamplingResult(SamplingDecision.Drop)
            : Delegado.ShouldSample(in samplingParameters);
}
