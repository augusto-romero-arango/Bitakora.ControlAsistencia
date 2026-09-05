using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
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
                // Issue #309: apaga el polling de metricas de profundidad de cola de Wolverine
                // (PersistenceMetrics.StartPolling, PeriodicTimer de 5s que llama
                // store.Admin.FetchCountsAsync()). Este Function App aun no emite telemetria (no
                // llama UseAzureMonitorExporter, issue #308) y esta frio, asi que el polling no
                // aparece en Application Insights hoy, pero corre igual cada 5s y carga Postgres --
                // se apaga aqui para que el dominio no quede con la regresion latente esperando a
                // que se instrumente.
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
                // ADR-0024 decision #3: aunque ProgramacionTurnoDiarioSolicitada es IPrivateEvent (issue #210),
                // sigue cruzando fisicamente el ASB interno del BC. El topic mapping se conserva: el constraint
                // de PublicarEventoServerless es IEvent y Wolverine rutea por el tipo concreto del mensaje, asi
                // que el mismo mapeo aplica ya sea que se publique via IPublicEventSender o IPrivateEventSender.
                options.PublicarEventoServerless<ProgramacionTurnoDiarioSolicitada>(
                    "programacion-turno-diario-solicitada");
                options.PublicarEventoServerless<CancelacionTurnoDiarioSolicitada>(
                    "cancelacion-turno-diario-solicitada");
            });

        services.AgregarMartenEventStore();
        // Tenancy (MEF-ADR-0028 etapa b): identidad ambiente por AsyncLocal, poblada por
        // TenantContextMiddleware en Program.cs (patron de Cosmos.ControlPlane, MEF-ADR-0032). NO usar
        // AgregarTenantResolverHibrido(): su ProxyTenantResolver decide la rama en el constructor
        // segun IHttpContextAccessor.HttpContext, que es null cuando el grafo de DI lo construye en el
        // worker aislado -- toda request HTTP caia en la rama de Wolverine y fallaba (hotfix 2026-09-01).
        services.AgregarTenantResolverControlAsistencia();
        services.AgregarWolverineCommandRouter();
        services.AgregarWolverineEventSender();
        services.AddScoped<ILectorNombresTurno, LectorReadSideProgramacion>();
        // Issue #626: mismo adaptador (LectorReadSideProgramacion), segundo puerto -- un
        // adaptador por store/tenant con dos metodos, espejo del registro de arriba.
        services.AddScoped<ILectorNombresPlantillaSemanal, LectorReadSideProgramacion>();

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
            options.Events.AddEventTypes(IdentidadEventosProgramacion.TiposPersistidos);

            // Par 2 de compatibilidad write-side/read-side (MEF-ADR-0034 seccion 6). Marten aplica
            // ProjectionDocumentPolicy -- mt_version bigint, incondicional
            // (https://martendb.io/documents/concurrency) -- a todo documento target de una
            // proyeccion registrada. FichaTurnoProjection vive en el worker (CA-ADR-0029), asi que
            // este store no puede registrarla y sin esta linea esperaria mt_version uuid sobre la
            // MISMA tabla fisica: "alter column" en CADA request, rechazado por Postgres con 42804,
            // y los GET en 500 permanente. El par de config-tests congela ambos literales.
            options.Schema.For<FichaTurno>().UseNumericRevisions(true);

            // Issue #625: par 2 para CuadroSemanalTurnos -- este GET (ObtenerCuadroSemanalTurnos/
            // ListarCuadrosSemanalesTurnos) es el primer consumidor write-side de la vista que #624
            // materializo. Mismo motivo que FichaTurno arriba: sin esta linea el store esperaria
            // mt_version uuid sobre la MISMA tabla que el worker ya escribe con mt_version bigint,
            // 500 permanente en el primer request real.
            options.Schema.For<CuadroSemanalTurnos>().UseNumericRevisions(true);

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

        // Serializacion JSON global: camelCase hacia el cliente, case-insensitive en lectura
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
        });

        // Validacion de requests
        services.AddScoped<IRequestValidator, RequestValidator>();
        services.AddValidatorsFromAssemblyContaining<IProgramacionAssemblyMarker>();

        // Issue #399: readiness gate del event store, consumido por ReadyCheck (GET /api/ready).
        services.AddScoped<IEventStoreReadinessProbe, EventStoreReadinessProbe>();

        return services;
    }
}
