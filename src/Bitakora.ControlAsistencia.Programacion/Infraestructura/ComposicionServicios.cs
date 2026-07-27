using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos;
using Cosmos.EventDriven.CritterStack;
using Cosmos.EventDriven.CritterStack.AzureServiceBus;
using Cosmos.EventSourcing.CritterStack;
using Cosmos.EventSourcing.CritterStack.Commands;
using Cosmos.MultiTenancy;
using FluentValidation;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Issue #221: composicion del contenedor DI extraida de Program.cs a un metodo testeable. Unica
// fuente de verdad: Program.cs y el test de composicion (Programacion.Tests) invocan este mismo
// metodo, asi que un wiring roto (p.ej. el hueco de ITenantResolver de #219) no puede
// desincronizarse entre el host real y el guardrail de CI.
public static class ComposicionServicios
{
    public static IServiceCollection AgregarServiciosProgramacion(
        this IServiceCollection services,
        string martenConnectionString,
        string serviceBusConnectionString,
        bool isDev)
    {
        services.AgregarWolverineParaComandosServerless(
            typeof(IProgramacionAssemblyMarker).Assembly,
            martenConnectionString,
            "programacion",
            isDev,
            options =>
            {
                options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);
                // ADR-0024 decision #3: aunque ProgramacionTurnoDiarioSolicitada es IPrivateEvent (issue #210),
                // sigue cruzando fisicamente el ASB interno del BC. El topic mapping se conserva: el constraint
                // de PublicarEventoServerless es IEvent y Wolverine rutea por el tipo concreto del mensaje, asi
                // que el mismo mapeo aplica ya sea que se publique via IPublicEventSender o IPrivateEventSender.
                options.PublicarEventoServerless<ProgramacionTurnoDiarioSolicitada>(
                    "programacion-turno-diario-solicitada");
            });

        services.AgregarMartenEventStore();
        // Issue #219: Cosmos.Event* 2.x dejo de auto-registrar un ITenantResolver por defecto (se movio a
        // Cosmos.MultiTenancy.CritterStack), pero los routers/senders de Wolverine lo siguen exigiendo por
        // constructor. Este proyecto es mono-tenant: se registra un resolver de valores fijos en vez de los
        // resolvers header-based de 2.x. Ver docs/adr/ca-adr-0027-estrategia-tenancy-mono-tenant.md.
        services.AddScoped<ITenantResolver, TenantResolverFijo>();
        services.AgregarWolverineCommandRouter();
        services.AgregarWolverineEventSender();

        // Registrar serializacion custom para tipos con constructores privados
        services.ConfigureMarten(options =>
        {
            if (options.Serializer() is Marten.Services.SystemTextJsonSerializer stj)
            {
                stj.Configure(jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    SubFranja.ConfigurarSerializacion(resolver);
                    FranjaOrdinaria.ConfigurarSerializacion(resolver);
                    TurnoCreado.ConfigurarSerializacion(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });
            }

            // Issue #232 (MEF-ADR-0034 seccion 7): Marten deja estas tres columnas de metadata de
            // evento deshabilitadas por defecto -- sin este opt-in explicito, la columna ni
            // siquiera se crea en la tabla de eventos, y ninguna proyeccion futura (Inline/Async)
            // puede leer un CorrelationId/CausationId/header que nunca se persistio. Requisito del
            // writer, independiente de que este dominio tenga o no un middleware de trazas activo.
            options.Events.MetadataConfig.CorrelationIdEnabled = true;
            options.Events.MetadataConfig.CausationIdEnabled = true;
            options.Events.MetadataConfig.HeadersEnabled = true;
        });

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Wolverine")
                .AddSource("Marten")
                .AddSource("Bitakora.ControlAsistencia.Programacion.*"));

        // Serializacion JSON global: camelCase hacia el cliente, case-insensitive en lectura
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
        });

        // Validacion de requests
        services.AddScoped<IRequestValidator, RequestValidator>();
        services.AddValidatorsFromAssemblyContaining<IProgramacionAssemblyMarker>();

        return services;
    }
}
