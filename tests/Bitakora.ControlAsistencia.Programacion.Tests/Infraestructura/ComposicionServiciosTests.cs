// Issue #221: guardrail de CI que compone el contenedor DI real del dominio (el mismo metodo que
// invoca Program.cs, ver ComposicionServicios.AgregarServiciosProgramacion) y valida que el grafo
// completo es resoluble, sin infra desplegada (connection strings dummy).
//
// Root cause que cierra este guardrail (issue #219): el upgrade de Cosmos.Event* 0.1.9 -> 2.1.0
// (issue #207) dejo de auto-registrar un ITenantResolver, y ni "dotnet build" ni los tests
// unitarios existentes construyen el grafo de DI del host, asi que el hueco solo se detecto
// DESPUES del deploy, en los smoke tests contra dev (HTTP 500 en toda funcion).
//
// Programacion no registra IPrivateEventRouter ni IQueryRouter hoy (ver Notas tecnicas del
// issue #221): solo se resuelve explicitamente ICommandRouter, el unico router critico
// efectivamente registrado en este dominio.
//
// Issue #232 (MEF-ADR-0034 seccion 7): Marten deja deshabilitadas por defecto las tres columnas
// de metadata de evento (CorrelationId/CausationId/Headers) -- sin opt-in explicito la columna ni
// siquiera se crea en la tabla de eventos. Este test verifica el opt-in sobre el IDocumentStore
// resuelto del contenedor real (sin Postgres: el DocumentStore no abre conexion en bootstrap,
// solo en la primera operacion real -- Marten 7+). La cadena de lectura es de solo lectura y sin
// downcast: IDocumentStore.Options (IReadOnlyStoreOptions) -> Events (IReadOnlyEventStoreOptions)
// -> MetadataConfig (IReadonlyMetadataConfig).

using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.EventSourcing.Abstractions.Commands;
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*: vive en JasperFx.MultiTenancy)
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using ObtenerFichaTurnoEndpoint = Bitakora.ControlAsistencia.Programacion.ObtenerFichaTurno.FunctionEndpoint;
using ListarFichasTurnoEndpoint = Bitakora.ControlAsistencia.Programacion.ListarFichasTurno.FunctionEndpoint;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class ComposicionServiciosTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    private static ServiceProvider ComponerServiceProvider()
    {
        var services = new ServiceCollection();

        services.AgregarServiciosProgramacion(
            MartenConnectionStringDummy,
            ServiceBusConnectionStringDummy,
            isDev: true);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void AgregarServiciosProgramacion_ComponeElGrafoCompleto_CuandoLasConnectionStringsSonDummy()
    {
        var act = () => ComponerServiceProvider().Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosProgramacion_ResuelveICommandRouter_CuandoElContenedorEstaCompuesto()
    {
        // Wolverine registra dependencias scoped que solo implementan IAsyncDisposable
        // (Wolverine.Persistence.MessageStoreCollection); el scope se libera con DisposeAsync.
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ICommandRouter>();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosProgramacion_HabilitaColumnasDeMetadataDeEvento_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var metadataConfig = store.Options.Events.MetadataConfig;

        metadataConfig.CorrelationIdEnabled.Should().BeTrue();
        metadataConfig.CausationIdEnabled.Should().BeTrue();
        metadataConfig.HeadersEnabled.Should().BeTrue();
    }

    // Issue #232 CA-5: las tres banderas de metadata comparten el mismo callback ConfigureMarten que
    // registra el resolver de serializacion custom, asi que un edit futuro de ese bloque puede tumbar
    // la serializacion sin que el test de metadata se ponga rojo. Los round-trip existentes
    // (TurnoCreadoSerializacionTests) NO cubren este riesgo: replican las opciones de Marten a mano
    // -- una ruta paralela que no atraviesa el contenedor. Este test ejercita el ISerializer que el
    // store realmente compuso. Importa porque el `if (options.Serializer() is SystemTextJsonSerializer)`
    // del wiring omite el resolver EN SILENCIO si el serializador deja de ser STJ, y SubFranja
    // (campos privados, sin propiedades publicas) no sobrevive STJ vanilla.
    [Fact]
    public async Task AgregarServiciosProgramacion_ConservaLaSerializacionCustom_CuandoTambienHabilitaMetadataDeEvento()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var serializador = scope.ServiceProvider.GetRequiredService<IDocumentStore>().Options.Serializer();
        var original = SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(16, 0));

        using var json = new MemoryStream(Encoding.UTF8.GetBytes(serializador.ToJson(original)));
        var restaurado = serializador.FromJson<SubFranja>(json);

        restaurado.Should().Be(original);
        restaurado.ToString().Should().Be(original.ToString());
    }

    // Issue #277 CA-2/CA-4: el #237 movio los eventos persistidos a este ensamblado (namespace y
    // assembly nuevos) sin registrar su tipo en el EventGraph. Toda lectura quedo dependiendo del
    // fallback por mt_dotnet_type -- roto para el nuevo namespace -- en vez de resolver por alias
    // (columna "type" de mt_events). Este guardrail detecta el olvido sobre el store real que
    // compone el contenedor (mismo store que ya usan las guardas de arriba), sin Postgres.
    //
    // Los tipos esperados se listan literalmente (oraculo independiente, MEF-ADR-0002): leerlos de
    // IdentidadEventosProgramacion.TiposPersistidos acoplaria el guardrail al mismo artefacto que
    // IdentidadEventosProgramacionTests ya verifica, y la asercion pasaria en verde aunque la lista
    // quedara vacia.
    [Fact]
    public async Task AgregarServiciosProgramacion_RegistraLosTiposDeEventoPersistidos_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertEventosPersistidosRegistrados([typeof(TurnoCreado), typeof(ProgramacionTurnoSolicitada)]);
    }

    // Issue #277 CA-5/CA-7/CA-8: registrar el tipo solo sirve si el alias sigue siendo el que las
    // filas ya escritas llevan en su columna "type". AliasEventosProgramacionTests lo congela sobre
    // un StoreOptions standalone; esta guarda lo congela sobre el store del contenedor, el unico
    // lugar donde un MapEventType o un EventNamingStyle agregados al wiring podrian cambiarlo.
    [Fact]
    public async Task AgregarServiciosProgramacion_DerivaElAliasDeEventoDelNombreDeClase_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertAliasDeEventosPersistidos(new Dictionary<Type, string>
        {
            [typeof(TurnoCreado)] = "turno_creado",
            [typeof(ProgramacionTurnoSolicitada)] = "programacion_turno_solicitada"
        });
    }

    // --- Issue #309: apagar la recoleccion de metricas de durabilidad de Wolverine (CA-2, CA-3) ---
    //
    // Mismo wiring que ControlHoras (AgregarWolverineParaComandosServerless): Programacion no emite
    // telemetria hoy (no llama UseAzureMonitorExporter, issue #308) y ademas esta frio, asi que el
    // polling de FetchCountsAsync no aparece en Application Insights -- pero corre igual cada 5s y
    // carga Postgres. El issue #309 alcanza a los DOS Function Apps para que el dominio no quede con
    // la regresion latente esperando a que se instrumente.
    //
    // Se resuelve WolverineOptions del CONTENEDOR REAL (no un DurabilitySettings construido a mano):
    // el gotcha de wiring verificado en el issue es de ORDEN -- dentro de
    // AgregarWolverineParaComandosServerless el callback del consumidor corre PRIMERO y
    // options.Durability.Mode = Solo se asigna DESPUES, pisando cualquier intento de tocar Mode
    // desde el callback, pero NO pisa DurabilityMetricsEnabled. Ver el guardrail hermano en
    // ControlHoras.Tests para el detalle completo de FetchCountsAsync/PersistenceMetrics.
    //
    // El provider se libera con await using porque WolverineOptions se registra Singleton via
    // factory-lambda -- el contenedor lo trackea para disposal -- y solo implementa IAsyncDisposable:
    // el Dispose() sincrono del provider raiz lanzaria InvalidOperationException al encontrarlo entre
    // sus disposables. Mismo motivo por el que los tests de ICommandRouter/IPrivateEventRouter de
    // este archivo usan await using.
    [Fact]
    public async Task AgregarServiciosProgramacion_ApagaLaRecoleccionDeMetricasDeDurabilidad_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityMetricsEnabled.Should().BeFalse();
    }

    // CA-3: este issue apaga la recoleccion de METRICAS de cola, no la durabilidad real -- recovery,
    // scheduled jobs y dead letters siguen activos. Fijar Mode en el mismo test que
    // DurabilityMetricsEnabled evita que una futura correccion de CA-2 se resuelva apagando la
    // durabilidad completa en vez de solo la bandera de metricas -- Mode sigue siendo Solo, el valor
    // que AgregarWolverineParaComandosServerless fija incondicionalmente despues del callback.
    [Fact]
    public async Task AgregarServiciosProgramacion_ConservaLaDurabilidadReal_CuandoApagaLasMetricasDeCola()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityAgentEnabled.Should().BeTrue();
        opciones.Durability.Mode.Should().Be(DurabilityMode.Solo);
    }

    // Issue #399: test de composicion del tercer endpoint HTTP de operacion (mismo patron que las
    // guardas de arriba para las Functions GET de lectura) -- ReadyCheck depende de
    // IEventStoreReadinessProbe, hoy sin registrar en AgregarServiciosProgramacion.
    [Fact]
    public async Task AgregarServiciosProgramacion_ResuelveElEndpointDeReady_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ReadyCheck>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // ActivatorUtilities.CreateInstance reproduce la activacion por tipo del host de Azure
    // Functions isolated worker, para el que no existe un WebApplicationFactory (MEF-ADR-0029,
    // Alt 1). Solo cubre la RESOLUCION del constructor, no el comportamiento de Run.
    [Fact]
    public async Task AgregarServiciosProgramacion_ResuelveElEndpointDeObtenerFichaTurno_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ObtenerFichaTurnoEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosProgramacion_ResuelveElEndpointDeListarFichasTurno_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarFichasTurnoEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Mitad write-side del par 2 (MEF-ADR-0034 seccion 6): este Function App LEE FichaTurno sin
    // registrar la proyeccion, mientras el worker la MATERIALIZA en otro proceso sobre la misma
    // tabla. Sin Schema.For<FichaTurno>().UseNumericRevisions este store espera mt_version uuid
    // sobre una tabla bigint y los GET quedan en 500 permanente. Oraculo literal espejo del que
    // congela ConfiguracionMartenProjectionsTests, sin que un ensamblado referencie al otro.
    [Fact]
    public async Task AgregarServiciosProgramacion_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaFichaTurno()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaTurno));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Segunda dimension del mismo par 2: tabla, tenancy e IdMember tienen que converger entre el
    // worker que materializa y este Function App que consulta, o el GET responde 404 para siempre
    // con el daemon funcionando. Son dos configuraciones de Marten independientes sobre el mismo
    // schema: ningun compilador lo garantiza.
    [Fact]
    public async Task AgregarServiciosProgramacion_ResuelveFichaTurnoSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaTurno));

        mapping.TableName.QualifiedName.Should().Be("programacion.mt_doc_fichaturno");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(FichaTurno.Id));
    }
}
