// Issue #455 (replica del patron fijado en issue #221/#360 para Programacion/ControlHoras/
// Colaboradores): guardrail de CI que compone el contenedor DI real del dominio (el mismo metodo
// que invoca Program.cs, ver ComposicionServicios.AgregarServiciosSedes) y valida que el grafo
// completo es resoluble, sin infra desplegada (connection strings dummy).
//
// Root cause que este guardrail atrapa (issue #219): un ITenantResolver sin registrar compila y
// pasa los tests unitarios existentes -- ni "dotnet build" ni un test que no construya el grafo de
// DI del host detectan el hueco -- y solo aparece DESPUES del deploy, en los smoke tests contra
// dev (HTTP 500 en toda funcion).
//
// Limite conocido: BuildServiceProvider(ValidateOnBuild) no valida registros por factory-lambda
// (Wolverine/Marten registran varios), solo los de tipo mapeado -- de ahi la resolucion explicita
// de ICommandRouter en un scope, ademas del build general.

using System.Globalization;
using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*: vive en JasperFx.MultiTenancy)
using Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;
using Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Wolverine;
using ObtenerFichaSedeEndpoint = Bitakora.ControlAsistencia.Sedes.ObtenerFichaSede.FunctionEndpoint;
using ListarFichasSedeEndpoint = Bitakora.ControlAsistencia.Sedes.ListarFichasSede.FunctionEndpoint;
using ResolverSedeDeMarcacionEndpoint =
    Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado.FunctionEndpoint;
using InstalarDispositivoHandler =
    Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction.CommandHandler.InstalarDispositivoCommandHandler;

namespace Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;

public class ComposicionServiciosTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    // Issue #308 (replicado): nombre de la variable de entorno y default del ratio de sampling
    // (CA-ADR-0009 Capa 2). Se repite como literal inline en ComposicionServicios.cs -- a
    // diferencia de Projections, este dominio no declara InternalsVisibleTo hacia su proyecto de
    // tests -- por eso se fijan aqui una sola vez para no repetirlos en cada test.
    private const string VariableRatioSampling = "TELEMETRY_SAMPLING_RATIO";
    // Issue #515: la variable que el exporter lee para su ConnectionString. El seam nunca la lee
    // (MEF-ADR-0025: el secreto no pasa por el codigo del dominio); se nombra aqui solo para que el
    // guardrail del PostConfigure pueda simular su presencia.
    private const string VariableConnectionStringAppInsights = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    private const double RatioSamplingPorDefecto = 0.2;

    private static ServiceProvider ComponerServiceProvider()
    {
        var services = new ServiceCollection();

        services.AgregarServiciosSedes(
            MartenConnectionStringDummy,
            ServiceBusConnectionStringDummy,
            isDev: true);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    // Fija el valor de la variable de entorno mientras corre la accion y lo restaura despues (null
    // la elimina). Necesario para que el escenario de ratio configurado sea determinista frente a
    // tests corriendo en paralelo.
    private static void ConVariableDeEntorno(string nombre, string? valor, Action accion)
    {
        var original = Environment.GetEnvironmentVariable(nombre);
        Environment.SetEnvironmentVariable(nombre, valor);
        try
        {
            accion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(nombre, original);
        }
    }

    // Issue #308 (replicado): lee la propiedad interna `Sampler` de TracerProviderSdk
    // (OpenTelemetry.dll no la expone publicamente). Determinista -- compara tipos y lee
    // Sampler.Description (publica) --, no requiere muestrear actividades reales contra un ratio
    // fraccionario.
    private static Sampler ObtenerSamplerEfectivo(TracerProvider tracerProvider)
    {
        var propiedad = tracerProvider.GetType()
            .GetProperty("Sampler", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "TracerProviderSdk ya no expone la propiedad interna 'Sampler' " +
                "(OpenTelemetry 1.16.0) -- actualizar este helper de reflection.");

        return (Sampler)propiedad.GetValue(tracerProvider)!;
    }

    [Fact]
    public void AgregarServiciosSedes_ComponeElGrafoCompleto_CuandoLasConnectionStringsSonDummy()
    {
        var act = () => ComponerServiceProvider().Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosSedes_ResuelveICommandRouter_CuandoElContenedorEstaCompuesto()
    {
        // Wolverine registra dependencias scoped que solo implementan IAsyncDisposable
        // (Wolverine.Persistence.MessageStoreCollection); el scope se libera con DisposeAsync.
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ICommandRouter>();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosSedes_HabilitaColumnasDeMetadataDeEvento_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var metadataConfig = store.Options.Events.MetadataConfig;

        metadataConfig.CorrelationIdEnabled.Should().BeTrue();
        metadataConfig.CausationIdEnabled.Should().BeTrue();
        metadataConfig.HeadersEnabled.Should().BeTrue();
    }

    // --- Sampler efectivo del TracerProvider (issue #308, replicado) ---
    [Fact]
    public void AgregarServiciosSedes_ResuelveElSamplerConfiguradoPorElProyecto_EnVezDeRateLimitedSampler()
    {
        // TracerProvider se registra Singleton (default de OpenTelemetry): no requiere un scope
        // Wolverine (IAsyncDisposable) para resolverse, a diferencia de ICommandRouter arriba --
        // Dispose() sincrono del ServiceProvider raiz basta.
        using var provider = ComponerServiceProvider();
        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        var samplerEfectivo = ObtenerSamplerEfectivo(tracerProvider);

        // Oraculo por nombre completo, no por referencia al tipo (es internal en otro ensamblado:
        // Azure.Monitor.OpenTelemetry.Exporter.Internals.RateLimitedSampler no se puede nombrar
        // desde este proyecto).
        samplerEfectivo.GetType().FullName.Should().NotBe(
            "Azure.Monitor.OpenTelemetry.Exporter.Internals.RateLimitedSampler");
        samplerEfectivo.Should().BeOfType<ParentBasedSampler>();
    }

    [Fact]
    public void AgregarServiciosSedes_PropagaElRatioDeSamplingConfigurado_AlSamplerEfectivo()
    {
        ConVariableDeEntorno(VariableRatioSampling, "0.5", () =>
        {
            using var provider = ComponerServiceProvider();
            var tracerProvider = provider.GetRequiredService<TracerProvider>();

            var samplerEfectivo = ObtenerSamplerEfectivo(tracerProvider);

            samplerEfectivo.Description.Should().Contain(FormatearRatio(0.5));
        });
    }

    [Fact]
    public void AgregarServiciosSedes_PropagaElRatioPorDefecto_AlSamplerEfectivo_CuandoLaVariableEstaAusente()
    {
        ConVariableDeEntorno(VariableRatioSampling, null, () =>
        {
            using var provider = ComponerServiceProvider();
            var tracerProvider = provider.GetRequiredService<TracerProvider>();

            var samplerEfectivo = ObtenerSamplerEfectivo(tracerProvider);

            samplerEfectivo.Description.Should().Contain(FormatearRatio(RatioSamplingPorDefecto));
        });
    }

    // TraceIdRatioBasedSampler embebe el ratio en su Description con formato F6 invariante
    // ("TraceIdRatioBasedSampler{0.200000}"), verificado contra OpenTelemetry 1.16.0.
    private static string FormatearRatio(double ratio) =>
        ratio.ToString("F6", CultureInfo.InvariantCulture);

    // --- Issue #309 (replicado): apagar la recoleccion de metricas de durabilidad de Wolverine ---
    [Fact]
    public async Task AgregarServiciosSedes_ApagaLaRecoleccionDeMetricasDeDurabilidad_CuandoElContenedorEstaCompuesto()
    {
        // El provider se libera con await using porque WolverineOptions se registra Singleton via
        // factory-lambda -- el contenedor lo trackea para disposal -- y solo implementa
        // IAsyncDisposable.
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityMetricsEnabled.Should().BeFalse();
    }

    // Este issue apaga la recoleccion de METRICAS de cola, no la durabilidad real -- recovery,
    // scheduled jobs y dead letters (procesados por el mismo DurabilityAgent) siguen activos. Mode
    // sigue siendo Solo, el valor que AgregarWolverineParaComandosServerless fija
    // incondicionalmente DESPUES del callback del consumidor.
    [Fact]
    public async Task AgregarServiciosSedes_ConservaLaDurabilidadReal_CuandoApagaLasMetricasDeCola()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityAgentEnabled.Should().BeTrue();
        opciones.Durability.Mode.Should().Be(DurabilityMode.Solo);
    }

    // Issue #399 (replicado): test de composicion del endpoint HTTP de operacion ReadyCheck --
    // depende de IEventStoreReadinessProbe, registrado en AgregarServiciosSedes desde el scaffold
    // (issue #455), a diferencia de Colaboradores donde se sumo en un issue posterior (#399).
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveElEndpointDeReady_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ReadyCheck>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Issue #456 (patron replicado de Colaboradores #330 / ControlHoras-Programacion #277 CA-2/
    // CA-4): SedeRegistrada nace con este issue en Sedes.DomainEvents sin registrar su tipo en el
    // EventGraph. Toda lectura quedaria dependiendo del fallback por mt_dotnet_type en vez de
    // resolver por alias (columna "type" de mt_events). Este guardrail detecta el olvido sobre el
    // store real que compone el contenedor, sin Postgres.
    //
    // El tipo esperado se lista literalmente (oraculo independiente, MEF-ADR-0002): leerlo de
    // IdentidadEventosSedes.TiposPersistidos acoplaria el guardrail al mismo artefacto que
    // AliasEventosSedesTests ya verifica, y la asercion pasaria en verde aunque la lista quedara
    // vacia.
    [Fact]
    public async Task AgregarServiciosSedes_RegistraLosTiposDeEventoPersistidos_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertEventosPersistidosRegistrados(
            [
                typeof(SedeRegistrada), typeof(NombreSedeModificado), typeof(UbicacionActualizada),
                typeof(CentroDeCostosAsignado), typeof(CentroDeCostosRetirado),
                typeof(SedeActivada), typeof(SedeDesactivada),
                typeof(DispositivoInstalado), typeof(DispositivoRetirado)
            ]);
    }

    // Issue #456: registrar el tipo solo sirve si el alias sigue siendo el que las filas ya
    // escritas llevan en su columna "type". AliasEventosSedesTests lo congela sobre un StoreOptions
    // standalone; esta guarda lo congela sobre el store del contenedor, el unico lugar donde un
    // MapEventType o un EventNamingStyle agregados al wiring podrian cambiarlo.
    [Fact]
    public async Task AgregarServiciosSedes_DerivaElAliasDeEventoDelNombreDeClase_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertAliasDeEventosPersistidos(new Dictionary<Type, string>
        {
            [typeof(SedeRegistrada)] = "sede_registrada",
            [typeof(NombreSedeModificado)] = "nombre_sede_modificado",
            [typeof(UbicacionActualizada)] = "ubicacion_actualizada",
            [typeof(CentroDeCostosAsignado)] = "centro_de_costos_asignado",
            [typeof(CentroDeCostosRetirado)] = "centro_de_costos_retirado",
            [typeof(SedeActivada)] = "sede_activada",
            [typeof(SedeDesactivada)] = "sede_desactivada",
            [typeof(DispositivoInstalado)] = "dispositivo_instalado",
            [typeof(DispositivoRetirado)] = "dispositivo_retirado"
        });
    }

    // Issue #461: test de composicion de una Function GET, hermano de MEF-ADR-0029 -- misma idea
    // que las guardas de arriba (grafo de DI real, sin infra desplegada), pero sobre un
    // FunctionEndpoint en vez de un router de Wolverine. ActivatorUtilities.CreateInstance
    // reproduce la activacion por tipo que hace el host de Azure Functions isolated worker, sin
    // levantar el host real (Alt 1 de MEF-ADR-0029: no existe un WebApplicationFactory para
    // Functions isolated worker).
    //
    // Se prueba solo la RESOLUCION de IDocumentStore/ITenantResolver por constructor -- no el
    // comportamiento de Run (recomputar el stream key, session.LoadAsync y el 200/404, CA-5), que
    // es responsabilidad de projection-implementer y del smoke test. Por eso este test queda en
    // verde tan pronto exista el FunctionEndpoint stub con el constructor correcto -- no es la
    // guarda que fuerza el rojo de este issue (esa la dan los unit tests de la proyeccion y el
    // config-test del worker, en Projections.Tests).
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveElEndpointDeObtenerFichaSede_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ObtenerFichaSedeEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Hermano del de ObtenerFichaSede de arriba, para el listado (CA-6). Mismo alcance: solo la
    // RESOLUCION del constructor, no el filtro Activa opcional (MEF-ADR-0042 seccion 1) ni la
    // ausencia de paginacion (decision de sesion 2026-08-27, MEF-ADR-0018).
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveElEndpointDeListarFichasSede_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarFichasSedeEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Issue #461, mitad write-side del par 2 de compatibilidad write-side/read-side (MEF-ADR-0034
    // seccion 6). Heredada de la guarda que #356 dejo para FichaColaborador, por el incidente real
    // del issue #294: este Function App LEE FichaSede (ObtenerFichaSede, ListarFichasSede) sin
    // registrar la proyeccion, mientras el worker la MATERIALIZA en otro proceso sobre la misma
    // tabla fisica.
    //
    // Sin la declaracion explicita del lado lectura (Schema.For<FichaSede>().UseNumericRevisions),
    // este store espera mt_version uuid sobre la tabla que el worker creo como bigint, Marten
    // intenta "alter column" en CADA request y Postgres responde 42804: GET en 500 permanente con
    // el daemon funcionando.
    //
    // Oraculo literal, espejo del que ConfiguracionMartenProjectionsTests
    // .ConfigurarSedes_MaterializaFichaSedeConRevisionNumerica congela desde el worker, sin que
    // ningun ensamblado referencie al otro (CA-ADR-0029).
    [Fact]
    public async Task AgregarServiciosSedes_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaFichaSede()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaSede));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Issue #461, segunda dimension del mismo par 2 (precedente #356 sobre FichaColaborador): tabla,
    // tenancy e IdMember tienen que converger entre el worker que materializa y este Function App
    // que consulta, o el GET responde 404 para siempre con el daemon funcionando. Ningun compilador
    // lo garantiza -- son dos configuraciones de Marten independientes sobre el mismo schema.
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveFichaSedeSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaSede));

        mapping.TableName.QualifiedName.Should().Be("sedes.mt_doc_fichasede");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(FichaSede.Id));
    }

    // Issue #467: mismo par 2 para UbicacionDispositivo, que este Function App empieza a consultar
    // con la reaccion ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado. Hasta este issue
    // ningun proceso del write-side la leia, y el config-test del worker lo dejaba anotado como la
    // condicion pendiente: el dia que un Function App la consulte, ese lado declara
    // Schema.For<UbicacionDispositivo>().UseNumericRevisions(true) o su primera query dispara el
    // 42804 por request.
    //
    // Espejo de ConfiguracionMartenProjectionsTests (Projections.Tests)
    // .ConfigurarSedes_MaterializaUbicacionDispositivoConRevisionNumerica.
    [Fact]
    public async Task AgregarServiciosSedes_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaUbicacionDispositivo()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(UbicacionDispositivo));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Segunda dimension del mismo par 2 para UbicacionDispositivo: tabla, tenancy e IdMember. Una
    // divergencia aqui deja el lookup del dispositivo en null permanente con el daemon
    // materializando, y la reaccion loguearia "dispositivo desconocido" para dispositivos que si
    // existen.
    //
    // Espejo de ConfiguracionMartenProjectionsTests (Projections.Tests)
    // .ConfigurarSedes_MaterializaUbicacionDispositivoSobreLaTablaQueConsultaElWriteSide.
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveUbicacionDispositivoSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(UbicacionDispositivo));

        mapping.TableName.QualifiedName.Should().Be("sedes.mt_doc_ubicaciondispositivo");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(UbicacionDispositivo.Id));
    }

    // --- Reaccion de enriquecimiento coreografiado (issue #467, MEF-ADR-0046) ---

    // Primer consumo de evento privado del dominio: sin AgregarWolverinePrivateEventRouter() el
    // FunctionEndpoint compila igual y revienta al primer mensaje del bus (mismo guardrail que
    // ControlHoras tiene desde su primer suscriptor -- MEF-ADR-0029).
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveIPrivateEventRouter_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IPrivateEventRouter>();

        act.Should().NotThrow();
    }

    // El lector del read-side propio (los dos lookups de MEF-ADR-0046 paso 2) depende de
    // IDocumentStore + ITenantResolver por constructor: si alguno faltara, el hueco solo aparece al
    // procesar el primer mensaje.
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveElLectorDelReadSide_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ILectorSedesParaMarcacion>();

        act.Should().NotThrow();
    }

    // El handler de la reaccion lo activa el router, no el contenedor: ActivatorUtilities reproduce
    // esa activacion y valida sus tres dependencias (lector, IPrivateEventSender, ILogger<>).
    [Fact]
    public async Task AgregarServiciosSedes_ActivaElHandlerDeLaReaccion_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities
            .CreateInstance<RegistroDeMarcacionCreadoEventHandler>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Wolverine activa el CommandHandler, no el contenedor: ActivatorUtilities reproduce esa
    // activacion y valida sus dos dependencias (IEventStore, ILectorUbicacionDispositivo). Sin este
    // guardrail, un ILectorUbicacionDispositivo sin registrar compila y solo revienta en el primer
    // POST contra dev.
    [Fact]
    public async Task AgregarServiciosSedes_ActivaElHandlerDeInstalarDispositivo_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<InstalarDispositivoHandler>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // El worker de Functions activa el endpoint de ServiceBus por constructor, igual que los
    // endpoints HTTP: se valida la misma activacion que arma Program.cs.
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveElEndpointDeResolverSedeDeMarcacion_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities
            .CreateInstance<ResolverSedeDeMarcacionEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // --- Issue #515 / MEF-ADR-0038 seccion 9 (write-side, harness #700) ---

    // La garantia no es el comentario del seam: es este test, que resuelve el valor EFECTIVO que el
    // exporter usara. Con el flip en su default (true) el exporter instala LogFilteringProcessor y
    // descarta todo LogRecord emitido dentro de un span no muestreado; con
    // TELEMETRY_SAMPLING_RATIO fraccionario (0.2 por defecto en este dominio) eso pierde en silencio
    // los LogError que alimentan la alerta exception_spike (CA-ADR-0009 Capa 4).
    [Fact]
    public void AgregarServiciosSedes_DeshabilitaElSamplerDeLogsBasadoEnTrazas()
    {
        using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<IOptions<AzureMonitorExporterOptions>>().Value;

        opciones.EnableTraceBasedLogsSampler.Should().BeFalse();
    }

    // Issue #515: el seam agrega un PostConfigure que rellena ConnectionString con un valor inerte
    // cuando queda vacia, para que el reader de metricas de Azure Monitor (que se construye de forma
    // sincronica y exige una connection string) no tumbe el arranque en frio con la Key Vault
    // reference aun sin resolver. Ese fallback es seguro SOLO mientras no pise una connection string
    // real -- si la pisara, trazas y logs (que comparten AzureMonitorExporterOptions) se exportarian
    // a un endpoint inexistente, en silencio y de forma permanente: el peor modo de fallo posible
    // para telemetria. Nada en el codigo garantiza el orden Configure -> PostConfigure frente a un
    // upgrade del exporter que mueva la lectura de la variable a otro punto del pipeline; este test
    // es esa garantia.
    [Fact]
    public void AgregarServiciosSedes_ConservaLaConnectionStringReal_CuandoLaVariableDeEntornoEstaPresente()
    {
        const string connectionStringReal =
            "InstrumentationKey=11111111-2222-3333-4444-555555555555;" +
            "IngestionEndpoint=https://real.in.applicationinsights.azure.com/";

        ConVariableDeEntorno(VariableConnectionStringAppInsights, connectionStringReal, () =>
        {
            using var provider = ComponerServiceProvider();

            var opciones = provider.GetRequiredService<IOptions<AzureMonitorExporterOptions>>().Value;

            opciones.ConnectionString.Should().Be(connectionStringReal);
        });
    }
}
