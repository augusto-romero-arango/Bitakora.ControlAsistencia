using System.Text.Json;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PublicEvents.ControlHoras;
using Cosmos.EventDriven.CritterStack;
using Cosmos.EventDriven.CritterStack.AzureServiceBus;
using Cosmos.EventSourcing.CritterStack;
using Cosmos.EventSourcing.CritterStack.Commands;
using Cosmos.MultiTenancy;
using FluentValidation;
using Marten;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
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
                options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);
                // HU-108: registra el topic destino para DiaCalculado.
                // ADR-0004 + ADR-0005: un topic por evento, naming kebab-case en participio.
                options.PublicarEventoServerless<DiaCalculado>("dia-calculado");
                // issue #213 (ADR-0024 marco decision #3): MarcacionRegistrada es IPrivateEvent y debe
                // cruzar fisicamente el ASB interno del BC, aun siendo consumido dentro del mismo
                // Function App (AdicionarMarcacionCuandoMarcacionRegistrada). Topic + subscription
                // provisionados en #212 (infra/environments/dev/main.tf).
                options.PublicarEventoServerless<MarcacionRegistrada>("marcacion-registrada");
            });

        services.AgregarMartenEventStore();
        // Issue #219: Cosmos.Event* 2.x dejo de auto-registrar un ITenantResolver por defecto (se movio a
        // Cosmos.MultiTenancy.CritterStack), pero los routers/senders de Wolverine lo siguen exigiendo por
        // constructor. Este proyecto es mono-tenant: se registra un resolver de valores fijos en vez de los
        // resolvers header-based de 2.x. Ver docs/adr/ca-adr-0027-estrategia-tenancy-mono-tenant.md.
        services.AddScoped<ITenantResolver, TenantResolverFijo>();
        // AgregarWolverineCommandRouter se conserva: RegistrarMarcacion (HTTP) lo sigue usando.
        services.AgregarWolverineCommandRouter();
        // Issue #209/#210 (ADR-0024 decision #8): los eventos privados intra-BC (MarcacionRegistrada,
        // ProgramacionTurnoDiarioSolicitada) se consumen directo con IPrivateEventHandlerAsync via
        // IPrivateEventRouter, sin comando espejo.
        services.AgregarWolverinePrivateEventRouter();
        services.AgregarWolverineEventSender();

        // Registrar serializacion custom para tipos con constructores privados.
        // Issue #267: las tres columnas de metadata de evento que exige MEF-ADR-0034 seccion 7
        // (CorrelationId/CausationId/Headers) ya no se habilitan aqui -- las fija
        // AgregarConfiguracionMartenComandos desde Cosmos.EventSourcing.CritterStack v2.3.1, y
        // ComposicionServiciosTests lo verifica sobre el store real del contenedor.
        services.ConfigureMarten(options =>
        {
            if (options.Serializer() is Marten.Services.SystemTextJsonSerializer stj)
            {
                stj.Configure(jsonOptions =>
                {
                    var resolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionControlHoras.ConfigurarResolver(resolver);
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

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                .AddSource("Wolverine")
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource("Bitakora.ControlAsistencia.ControlHoras.*"))
            .UseFunctionsWorkerDefaults()
            .UseAzureMonitorExporter();

        // Serializacion JSON global: camelCase hacia el cliente, case-insensitive en lectura
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
        });

        // Validacion de requests
        services.AddScoped<IRequestValidator, RequestValidator>();
        services.AddValidatorsFromAssemblyContaining<IControlHorasAssemblyMarker>();

        return services;
    }
}
