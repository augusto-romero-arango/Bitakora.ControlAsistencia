using System.Text.Json;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.EventDriven.CritterStack;
using Cosmos.EventDriven.CritterStack.AzureServiceBus;
using Cosmos.EventSourcing.CritterStack;
using Cosmos.EventSourcing.CritterStack.Commands;
using Cosmos.MultiTenancy;
using FluentValidation;
using Marten;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

// Issue #221: composicion del contenedor DI extraida de Program.cs a un metodo testeable. Unica
// fuente de verdad: Program.cs y el test de composicion (ControlHoras.Tests) invocan este mismo
// metodo, asi que un wiring roto (p.ej. el hueco de ITenantResolver de #219) no puede
// desincronizarse entre el host real y el guardrail de CI.
public static class ComposicionServicios
{
    public static IServiceCollection AgregarServiciosControlHoras(
        this IServiceCollection services,
        string martenConnectionString,
        string serviceBusConnectionString,
        bool isDev)
    {
        services.AgregarWolverineParaComandosServerless(
            typeof(IControlHorasAssemblyMarker).Assembly,
            martenConnectionString,
            "control_horas",
            isDev,
            options =>
            {
                // Issue #309: apaga el polling de metricas de profundidad de cola de Wolverine
                // (PersistenceMetrics.StartPolling, PeriodicTimer de 5s que llama
                // store.Admin.FetchCountsAsync()) -- origen de 4 de las 8 consultas Postgres
                // repetitivas medidas en dev, 4.320 spans/6h cada una. Nadie las consume hoy (sin
                // dashboard ni alerta) y CheckHealthAsync llama FetchCountsAsync por su cuenta, asi
                // que el health check no depende de este polling.
                //
                // Se conserva la durabilidad real -- recovery, scheduled jobs y dead letters, que
                // corren en el mismo DurabilityAgent: DurabilityAgentEnabled y Mode no se tocan.
                //
                // Va en el callback de AgregarWolverineParaComandosServerless, y no despues de esa
                // llamada, porque es el hook que el paquete expone sobre WolverineOptions:
                // Cosmos.EventSourcing.CritterStack 2.3.1 lo corre PRIMERO y despues solo reasigna
                // Durability.Mode = Solo -- esa reasignacion pisaria en silencio cualquier intento
                // de tocar Mode desde aqui, pero no toca DurabilityMetricsEnabled (verificado
                // descompilando el paquete). Que sobreviva no es contrato del paquete: lo sostiene
                // el guardrail de ComposicionServiciosTests sobre el contenedor real.
                options.Durability.DurabilityMetricsEnabled = false;

                options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);
                // ADR-0004 + ADR-0005: un topic por evento, naming kebab-case en participio.
                options.PublicarEventoServerless<DiaDepurado>("dia-depurado");
                // issue #270 (ADR-0024 marco decision #3): RegistroDeMarcacionCreado (contrato de
                // bus, PrivateEvents.ControlHoras) es el IPrivateEvent que debe cruzar fisicamente el
                // ASB interno del BC, aun siendo consumido dentro del mismo Function App
                // (AdicionarMarcacionCuandoRegistroDeMarcacionCreado). MarcacionRegistrada (evento de
                // dominio persistido) ya no cruza el bus - dejo de implementar IPrivateEvent (CA-3).
                // Topic + subscription provisionados en #274 (infra/environments/dev/main.tf).
                options.PublicarEventoServerless<RegistroDeMarcacionCreado>("registro-de-marcacion-creado");
            });

        services.AgregarMartenEventStore();
        // Issue #219: Cosmos.Event* 2.x dejo de auto-registrar un ITenantResolver por defecto (se movio a
        // Cosmos.MultiTenancy.CritterStack), pero los routers/senders de Wolverine lo siguen exigiendo por
        // constructor. La infraestructura de este proyecto es multi-tenant conjoined (CA-ADR-0027) pero
        // opera con un unico tenant logico: se registra un resolver de valores fijos en vez de los
        // resolvers header-based de 2.x. Ver docs/adr/ca-adr-0027-tenancy-conjoined-con-tenant-unico.md.
        services.AddScoped<ITenantResolver, TenantResolverFijo>();
        // AgregarWolverineCommandRouter se conserva: RegistrarMarcacion (HTTP) lo sigue usando.
        services.AgregarWolverineCommandRouter();
        // Issue #209/#210/#270 (ADR-0024 decision #8): los eventos privados intra-BC
        // (RegistroDeMarcacionCreado, ProgramacionTurnoDiarioSolicitada) se consumen directo con
        // IPrivateEventHandlerAsync via IPrivateEventRouter, sin comando espejo.
        services.AgregarWolverinePrivateEventRouter();
        services.AgregarWolverineEventSender();

        // Registrar serializacion custom para tipos con constructores privados.
        // Issue #267: las tres columnas de metadata de evento que exige MEF-ADR-0034 seccion 7
        // (CorrelationId/CausationId/Headers) ya no se habilitan aqui -- las fija
        // AgregarConfiguracionMartenComandos desde Cosmos.EventSourcing.CritterStack v2.3.1, y
        // ComposicionServiciosTests lo verifica sobre el store real del contenedor.
        services.ConfigureMarten(options =>
        {
            // Issue #277: registra los tipos de evento persistidos en el EventGraph. No declara
            // alias -- Marten lo sigue derivando del nombre de clase -- solo garantiza que el
            // mapping exista antes de la primera lectura, en vez de depender de que un append lo
            // haya poblado (issue #237 seccion "Consecuencia asumida").
            options.Events.AddEventTypes(IdentidadEventosControlHoras.TiposPersistidos);

            // Issue #294 (aplicado a TurnoVigente por #328): declara del lado LECTURA la forma de
            // mt_version que el worker ya impuso del lado escritura -- el reverso del principio de
            // #268, donde el worker replicaba lo que el write-side poseia. Aqui la tabla la posee
            // la proyeccion, y este Function App solo la consulta (ObtenerTurnoVigente,
            // ListarTurnosVigentes).
            //
            // Marten aplica ProjectionDocumentPolicy a todo documento que sea target de una
            // proyeccion registrada en ese store: UseNumericRevisions = true, Metadata.Revision
            // (mt_version bigint) habilitada y Metadata.Version (mt_version uuid) DESHABILITADA. No
            // es opt-in ni depende de IRevisioned ni del lifecycle: la doc lo declara como la
            // excepcion a que el versionado sea opt-in
            // (https://martendb.io/documents/concurrency, "Numeric Revisioned Documents") y el
            // codigo lo hace incondicional (Marten/Events/Projections/ProjectionDocumentPolicy.cs).
            //
            // El worker registra TurnoVigenteProjection, asi que crea mt_version como bigint. Este
            // store NO la registra ni puede hacerlo -- TurnoVigenteProjection vive en el ensamblado
            // del worker y referenciarlo violaria CA-ADR-0029 --, asi que sin esta linea esperaria
            // mt_version uuid sobre la MISMA tabla fisica. Con AutoCreate en su default
            // CreateOrUpdate el sintoma no es un 404: Marten intenta "alter column mt_version type
            // uuid" en CADA request, Postgres lo rechaza con 42804 (no hay cast automatico
            // bigint -> uuid) y los GET responden 500 de forma permanente. Eso fue lo que ocurrio
            // en dev tras el deploy de #290, sobre el read model que #323 ya retiro.
            //
            // Se declara por documento y no via Policies para no alterar la forma de ningun otro
            // documento de este store. El par de config-tests (este lado y el del worker) congela
            // los mismos valores literales, que es la dimension que el par de #289 dejo abierta.
            options.Schema.For<TurnoVigente>().UseNumericRevisions(true);

            // Mismo caso que TurnoVigente arriba, para la proyeccion que el worker registra desde
            // el issue #441: quitar esta linea revive el "alter column" 42804 permanente descrito
            // arriba, ahora en ListarAsistenciasDiarias/ListarResumenesAsistencia.
            options.Schema.For<AsistenciaDiaria>().UseNumericRevisions(true);

            if (options.Serializer() is Marten.Services.SystemTextJsonSerializer stj)
            {
                stj.Configure(jsonOptions =>
                {
                    var resolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
                    // Issue #237: dos listas con responsabilidades distintas. La de DomainEvents es la
                    // que el worker de proyecciones puede replicar; la de calculo cubre los VOs que
                    // hoy no se persisten pero sostienen la barrera de #232 CA-5 sobre este mismo
                    // store (ComposicionServiciosTests usa IntervaloTemporal como canario).
                    ConfiguracionSerializacionControlHoras.ConfigurarResolver(resolver);
                    ConfiguracionSerializacionCalculoHoras.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });
            }
        });

        // Observabilidad: exporta las trazas del worker (Npgsql, Marten, Wolverine, handlers) a
        // Application Insights via OpenTelemetry. Sin esto el proceso worker no emite dependencies
        // ni el desglose de latencia por capa. Se usa el exporter -no el distro
        // Azure.Monitor.OpenTelemetry.AspNetCore- para evitar request telemetry duplicado en el worker.
        // Guia oficial: https://learn.microsoft.com/azure/azure-functions/opentelemetry-howto?pivots=programming-language-csharp
        //
        // ADR-0009 Capa 2 (control de costos): con telemetryMode=OpenTelemetry el sampling de
        // host.json (logging.applicationInsights) deja de aplicar, asi que el volumen de trazas se
        // controla aqui con un sampler OTel. Ratio configurable via la app setting
        // TELEMETRY_SAMPLING_RATIO (default 0.2); para un diagnostico puntual se sube a 1.0 y se baja
        // despues. El daily cap (Capa 3) sigue siendo el tope duro y la alerta de spike (Capa 4) sigue
        // activa. Nota: el sampling head-based tambien muestrea excepciones (a diferencia del sampling
        // adaptativo clasico que las preservaba al 100%); el cap y la alerta de spike lo compensan.
        var samplingRatio = double.TryParse(
            Environment.GetEnvironmentVariable("TELEMETRY_SAMPLING_RATIO"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var ratio) && ratio is >= 0.0 and <= 1.0
                ? ratio
                : 0.2;

        // Issue #308 (hallazgo 1): UseAzureMonitorExporter() llama SetSampler internamente
        // (Azure.Monitor.OpenTelemetry.Exporter 1.8.1) con RateLimitedSampler porque
        // AzureMonitorExporterOptions.TracesPerSecond tiene default 5.0. Si el SetSampler del
        // proyecto se escribe ANTES de UseAzureMonitorExporter() (como estaba), ese SetSampler
        // interno lo pisa y el sampler configurado aqui nunca se instala -- verificado en runtime.
        // La correccion es de ORDEN: un segundo .WithTracing(...) DESPUES de
        // .UseAzureMonitorExporter() para que el SetSampler de este seam sea el que gane.
        // A diferencia de Projections (que envuelve el sampler con SamplerQueDescartaPollingDelDaemon
        // porque el worker corre el daemon HotCold de Marten), ControlHoras no corre ningun daemon
        // -- MEF-ADR-0018 Rule of Three: el filtro tiene un solo consumidor real y no se generaliza
        // aqui.
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Wolverine")
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource("Bitakora.ControlAsistencia.ControlHoras.*"))
            .UseFunctionsWorkerDefaults()
            .UseAzureMonitorExporter()
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio))));

        // Serializacion JSON global: camelCase hacia el cliente, case-insensitive en lectura
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
        });

        // Validacion de requests
        services.AddScoped<IRequestValidator, RequestValidator>();
        services.AddValidatorsFromAssemblyContaining<IControlHorasAssemblyMarker>();

        // Issue #399: readiness gate del event store, consumido por ReadyCheck (GET /api/ready).
        services.AddScoped<IEventStoreReadinessProbe, EventStoreReadinessProbe>();

        return services;
    }
}
