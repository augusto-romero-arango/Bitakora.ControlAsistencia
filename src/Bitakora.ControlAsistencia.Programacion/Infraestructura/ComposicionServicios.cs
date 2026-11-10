using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.EventDriven.CritterStack;
using Cosmos.EventDriven.CritterStack.AzureServiceBus;
using Cosmos.EventSourcing.CritterStack;
using Cosmos.EventSourcing.CritterStack.Commands;
using Bitakora.ControlAsistencia.TenantResolver;
using FluentValidation;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Program.cs y el test de composicion invocan este mismo metodo: un wiring roto no puede
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
                // Apaga el polling de metricas de profundidad de cola de Wolverine (PeriodicTimer de 5 s
                // que llama store.Admin.FetchCountsAsync() y carga Postgres). La durabilidad real
                // (recovery, scheduled jobs, dead letters) se conserva: DurabilityAgentEnabled y Mode no
                // se tocan.
                //
                // Va en este callback porque Cosmos.EventSourcing.CritterStack 2.3.1 lo corre PRIMERO y
                // despues reasigna Durability.Mode = Solo (pisaria cualquier cambio de Mode hecho aqui),
                // pero no toca DurabilityMetricsEnabled (verificado descompilando el paquete). Que
                // sobreviva no es contrato del paquete: lo sostiene el guardrail de ComposicionServiciosTests.
                options.Durability.DurabilityMetricsEnabled = false;

                options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);
                options.PublicarEventoServerless<ProgramacionTurnoEnFechasSolicitada>(
                    "programacion-turno-en-fechas-solicitada");
                options.PublicarEventoServerless<CancelacionTurnoDiarioSolicitada>(
                    "cancelacion-turno-diario-solicitada");
                options.PublicarEventoServerless<AusenciaDiariaProgramada>(
                    "ausencia-diaria-programada");
                options.PublicarEventoServerless<AusenciaDiariaCancelada>(
                    "ausencia-diaria-cancelada");
                options.PublicarEventoServerless<LimitesDeJornadaActualizados>(
                    "limites-de-jornada-actualizados");
                options.PublicarEventoServerless<DisenoDeTurnoActualizado>(
                    "diseno-de-turno-actualizado");
            });

        services.AgregarMartenEventStore();
        // Identidad ambiente por AsyncLocal, poblada por TenantContextMiddleware (Program.cs). NO usar
        // AgregarTenantContextHibrido(): su ProxyTenantContext decide la rama en el constructor segun
        // IHttpContextAccessor.HttpContext, que es null en el worker aislado, y toda request HTTP caia en
        // la rama de Wolverine (CA-ADR-0032).
        services.AgregarTenantResolverControlAsistencia();
        services.AgregarWolverineCommandRouter();
        services.AgregarWolverineEventSender();
        services.AgregarWolverinePrivateEventRouter();
        services.AddScoped<ILectorNombresTurno, LectorReadSideProgramacion>();
        services.AddScoped<ILectorNombresPlantillaTurnos, LectorReadSideProgramacion>();
        services.AddScoped<ILectorLimitesJornada, LectorReadSideProgramacion>();
        services.AddScoped<ILectorPlantillasTurnosPorJornada, LectorReadSideProgramacion>();
        services.AddScoped<ILectorPlantillasTurnosPorTurno, LectorReadSideProgramacion>();
        services.AddScoped<IAlmacenPreferenciasProgramacion, AlmacenPreferenciasProgramacion>();
        services.AddScoped<IAseguradorJornadaPredeterminada, AseguradorJornadaPredeterminada>();

        // CorrelationId/CausationId/Headers (MEF-ADR-0034 seccion 7) no se habilitan aqui: las fija
        // AgregarConfiguracionMartenComandos (Cosmos.EventSourcing.CritterStack v2.3.1) y
        // ComposicionServiciosTests lo verifica sobre el store real.
        services.ConfigureMarten(options =>
        {
            options.Events.AddEventTypes(IdentidadEventosProgramacion.TiposPersistidos);

            // Par 2 de compatibilidad write-side/read-side (MEF-ADR-0034 seccion 6): Marten aplica
            // ProjectionDocumentPolicy (mt_version bigint, https://martendb.io/documents/concurrency) a
            // todo documento target de una proyeccion registrada. Las proyecciones viven en el worker, asi
            // que sin estas lineas este store esperaria mt_version uuid sobre la MISMA tabla: "alter
            // column" en cada request, rechazado por Postgres con 42804, y los GET en 500 permanente. El
            // par de config-tests congela ambos literales.
            options.Schema.For<FichaTurno>().UseNumericRevisions(true);

            options.Schema.For<CuadroDeTurnos>().UseNumericRevisions(true);

            options.Schema.For<AusenciaVigente>().UseNumericRevisions(true);

            options.Schema.For<LimitesDeJornada>().UseNumericRevisions(true);

            options.Schema.For<BorradorDeProgramacion>().UseNumericRevisions(true);

            if (options.Serializer() is Marten.Services.SystemTextJsonSerializer stj)
            {
                stj.Configure(jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionProgramacion.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });
            }
        });

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Wolverine")
                .AddSource("Marten")
                .AddSource("Bitakora.ControlAsistencia.Programacion.*"));

        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
        });

        services.AddScoped<IRequestValidator, RequestValidator>();
        services.AddValidatorsFromAssemblyContaining<IProgramacionAssemblyMarker>();

        services.AddScoped<IEventStoreReadinessProbe, EventStoreReadinessProbe>();

        return services;
    }
}
