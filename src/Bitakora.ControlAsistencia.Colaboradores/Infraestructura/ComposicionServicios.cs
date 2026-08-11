using System.Globalization;
using System.Text.Json;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
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

namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

// Issue #360: composicion del contenedor DI del dominio Colaboradores, extraida de Program.cs a un
// metodo testeable. Unica fuente de verdad: Program.cs y el test de composicion
// (Colaboradores.Tests) invocan este mismo metodo -- mismo patron que ComposicionServicios de
// Programacion/ControlHoras (issue #221), asi que un wiring roto (p.ej. el hueco de
// ITenantResolver de #219) no puede desincronizarse entre el host real y el guardrail de CI.
public static class ComposicionServicios
{
    public static IServiceCollection AgregarServiciosColaboradores(
        this IServiceCollection services,
        string martenConnectionString,
        string serviceBusConnectionString,
        bool isDev)
    {
        services.AgregarWolverineParaComandosServerless(
            typeof(IColaboradoresAssemblyMarker).Assembly,
            martenConnectionString,
            "colaboradores",
            isDev,
            options =>
            {
                // Issue #309 (replicado desde el scaffold del dominio -- Programacion/ControlHoras
                // ya lo aplican): apaga el polling de metricas de profundidad de cola de Wolverine
                // (PersistenceMetrics.StartPolling, PeriodicTimer de 5s que llama
                // store.Admin.FetchCountsAsync()). Nadie las consume hoy (sin dashboard ni alerta)
                // y CheckHealthAsync llama FetchCountsAsync por su cuenta, asi que el health check
                // no depende de este polling.
                //
                // Se conserva la durabilidad real -- recovery, scheduled jobs y dead letters, que
                // corren en el mismo DurabilityAgent: DurabilityAgentEnabled y Mode no se tocan.
                //
                // Va en el callback de AgregarWolverineParaComandosServerless, y no despues de esa
                // llamada, porque es el hook que el paquete expone sobre WolverineOptions:
                // Cosmos.EventSourcing.CritterStack 2.3.1 lo corre PRIMERO y despues solo reasigna
                // Durability.Mode = Solo -- esa reasignacion pisaria en silencio cualquier intento
                // de tocar Mode desde aqui, pero no toca DurabilityMetricsEnabled (verificado
                // descompilando el paquete en ControlHoras/ComposicionServicios). Que sobreviva no
                // es contrato del paquete: lo sostiene el guardrail de ComposicionServiciosTests
                // sobre el contenedor real.
                options.Durability.DurabilityMetricsEnabled = false;

                options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);

                // El dominio nace sin ningun evento de bus (issue #360: solo se genera el andamiaje
                // sobre el que se implementan los cortes #348/#353 y el resto del desglose). Cuando
                // Colaboradores publique su primer evento (privado o publico), se registra aqui con
                // options.PublicarEventoServerless<TEvento>("<topic>") -- ADR-0024 decision #3.
            });

        services.AgregarMartenEventStore();
        // Issue #219: Cosmos.Event* 2.x dejo de auto-registrar un ITenantResolver por defecto (se
        // movio a Cosmos.MultiTenancy.CritterStack), pero los routers/senders de Wolverine lo
        // siguen exigiendo por constructor. La infraestructura de este proyecto es multi-tenant
        // conjoined (CA-ADR-0027) pero opera con un unico tenant logico: se registra un resolver de
        // valores fijos en vez de los resolvers header-based de 2.x.
        // Ver docs/adr/ca-adr-0027-tenancy-conjoined-con-tenant-unico.md.
        services.AddScoped<ITenantResolver, TenantResolverFijo>();
        services.AgregarWolverineCommandRouter();
        services.AgregarWolverineEventSender();
        // Nota: AgregarWolverinePrivateEventRouter() no se registra todavia -- el dominio no
        // consume ningun evento privado por ahora (mismo criterio que Programacion). Se agrega
        // junto con el primer FunctionEndpoint de ServiceBus que este dominio necesite.

        services.ConfigureMarten(options =>
        {
            // Issue #277 (replicado desde el scaffold): registra los tipos de evento persistidos en
            // el EventGraph. Lista vacia al nacer -- se llena a medida que ColaboradorAggregateRoot
            // aplique sus primeros eventos.
            options.Events.AddEventTypes(IdentidadEventosColaboradores.TiposPersistidos);

            // Cuando aparezca un value object rico que necesite ConfigurarSerializacion (ctor
            // privado), se invoca AQUI DENTRO junto a AddEventTypes -- nunca en un ConfigureMarten
            // separado (issue #232 CA-5: ComposicionServiciosTests lo verifica sobre el store real
            // del contenedor, y un segundo ConfigureMarten corre el riesgo de que el resolver de
            // serializacion custom quede sin efecto en silencio si el orden de callbacks cambia).
        });

        // Observabilidad (issue #308, CA-ADR-0009 Capa 2): mismo wiring que ControlHoras -- el
        // dominio de referencia mas reciente en adoptar el exporter completo (a diferencia de
        // Programacion, que todavia no lo tiene). Ratio configurable via TELEMETRY_SAMPLING_RATIO
        // (default 0.2, igual que los otros dos dominios); el daily cap (Capa 3) y la alerta de
        // spike (Capa 4) siguen siendo el respaldo si el sampling deja pasar demasiado.
        var samplingRatio = double.TryParse(
            Environment.GetEnvironmentVariable("TELEMETRY_SAMPLING_RATIO"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var ratio) && ratio is >= 0.0 and <= 1.0
                ? ratio
                : 0.2;

        // Issue #308 hallazgo 1: UseAzureMonitorExporter() llama SetSampler internamente
        // (Azure.Monitor.OpenTelemetry.Exporter 1.8.1) con RateLimitedSampler porque
        // AzureMonitorExporterOptions.TracesPerSecond tiene default 5.0. El SetSampler propio DEBE
        // ir en un segundo .WithTracing(...) DESPUES de .UseAzureMonitorExporter() o ese
        // SetSampler interno lo pisa y el sampler configurado aqui nunca se instala (verificado en
        // runtime sobre ControlHoras).
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Wolverine")
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource("Bitakora.ControlAsistencia.Colaboradores.*"))
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
        services.AddValidatorsFromAssemblyContaining<IColaboradoresAssemblyMarker>();

        return services;
    }
}
