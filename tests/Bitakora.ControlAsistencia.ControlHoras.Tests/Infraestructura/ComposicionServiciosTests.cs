// Issue #221: guardrail de CI que compone el contenedor DI real del dominio (el mismo metodo que
// invoca Program.cs, ver ComposicionServicios.AgregarServiciosControlHoras) y valida que el grafo
// completo es resoluble, sin infra desplegada (connection strings dummy).
//
// Root cause que cierra este guardrail (issue #219): el upgrade de Cosmos.Event* 0.1.9 -> 2.1.0
// (issue #207) dejo de auto-registrar un ITenantResolver, y ni "dotnet build" ni los tests
// unitarios existentes construyen el grafo de DI del host, asi que el hueco solo se detecto
// DESPUES del deploy, en los smoke tests contra dev (HTTP 500 en toda funcion).
//
// Limite conocido: BuildServiceProvider(ValidateOnBuild) no valida registros por factory-lambda
// (Wolverine/Marten registran varios), solo los de tipo mapeado -- de ahi la resolucion explicita
// de ICommandRouter e IPrivateEventRouter en un scope, ademas del build general.
//
// Issue #232 (MEF-ADR-0034 seccion 7): Marten deja deshabilitadas por defecto las tres columnas
// de metadata de evento (CorrelationId/CausationId/Headers) -- sin opt-in explicito la columna ni
// siquiera se crea en la tabla de eventos. Este test verifica el opt-in sobre el IDocumentStore
// resuelto del contenedor real (sin Postgres: el DocumentStore no abre conexion en bootstrap,
// solo en la primera operacion real -- Marten 7+). La cadena de lectura es de solo lectura y sin
// downcast: IDocumentStore.Options (IReadOnlyStoreOptions) -> Events (IReadOnlyEventStoreOptions)
// -> MetadataConfig (IReadonlyMetadataConfig).

using System.Globalization;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using FluentValidation;
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*: vive en JasperFx.MultiTenancy)
using Marten;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Wolverine;
using ObtenerTurnoVigenteEndpoint = Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoVigente.FunctionEndpoint;
using ListarTurnosVigentesEndpoint = Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes.FunctionEndpoint;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class ComposicionServiciosTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    // Issue #308: nombre de la variable de entorno y default del ratio de sampling (CA-ADR-0009
    // Capa 2). Ambos se repiten como literal inline en ComposicionServicios.cs -- a diferencia de
    // Projections, que los extrae a constantes internas testeables -- porque este dominio no declara
    // InternalsVisibleTo hacia su proyecto de tests. Se fijan aqui una sola vez para no repetirlos
    // en cada test; si algun dia el dominio abre sus internals, estas dos constantes se reemplazan
    // por las de produccion.
    private const string VariableRatioSampling = "TELEMETRY_SAMPLING_RATIO";
    private const double RatioSamplingPorDefecto = 0.2;

    private static ServiceProvider ComponerServiceProvider()
    {
        var services = new ServiceCollection();

        services.AgregarServiciosControlHoras(
            MartenConnectionStringDummy,
            ServiceBusConnectionStringDummy,
            isDev: true);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    // Fija el valor de la variable de entorno mientras corre la accion y lo restaura despues (null
    // la elimina). Mismo patron que ConfiguracionObservabilidadProjectionsTests.ConVariableDeEntorno
    // (issue #250/#308): sin esto, el escenario de ratio configurado no seria determinista frente a
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

    // Issue #308: lee la propiedad interna `Sampler` de TracerProviderSdk (OpenTelemetry.dll no la
    // expone publicamente). Determinista -- compara tipos y lee Sampler.Description (publica) --,
    // no requiere muestrear actividades reales contra un ratio fraccionario. Duplicado deliberado
    // del mismo helper en Bitakora.ControlAsistencia.Projections.Tests (SamplerEfectivo.De): son
    // proyectos de test distintos, sin un ensamblado compartido entre ambos (CA-ADR-0029:
    // Contracts.Tests fue eliminado). Dos sitios -- MEF-ADR-0018 Rule of Three: se tolera la
    // duplicacion hasta que aparezca un tercer consumidor que justifique un ensamblado comun.
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
    public void AgregarServiciosControlHoras_ComponeElGrafoCompleto_CuandoLasConnectionStringsSonDummy()
    {
        var act = () => ComponerServiceProvider().Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveICommandRouter_CuandoElContenedorEstaCompuesto()
    {
        // Wolverine registra dependencias scoped que solo implementan IAsyncDisposable
        // (Wolverine.Persistence.MessageStoreCollection); el scope se libera con DisposeAsync.
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ICommandRouter>();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveIPrivateEventRouter_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IPrivateEventRouter>();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_HabilitaColumnasDeMetadataDeEvento_CuandoElContenedorEstaCompuesto()
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
    // (IntervaloTemporalSerializacionMartenTests) NO cubren este riesgo: usan
    // ConfiguracionSerializacionCalculoHoras.CrearOpcionesMarten() -- una ruta paralela que no
    // atraviesa el contenedor. Este test ejercita el ISerializer que el store realmente compuso.
    // Importa porque el `if (options.Serializer() is SystemTextJsonSerializer)` del wiring omite el
    // resolver EN SILENCIO si el serializador deja de ser STJ, e IntervaloTemporal (campos privados,
    // sin propiedades publicas) no sobrevive STJ vanilla.
    [Fact]
    public async Task AgregarServiciosControlHoras_ConservaLaSerializacionCustom_CuandoTambienHabilitaMetadataDeEvento()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var serializador = scope.ServiceProvider.GetRequiredService<IDocumentStore>().Options.Serializer();
        var original = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0)),
            new MomentoDelDia(new TimeOnly(17, 0)));

        using var json = new MemoryStream(Encoding.UTF8.GetBytes(serializador.ToJson(original)));
        var restaurado = serializador.FromJson<IntervaloTemporal>(json);

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
    // IdentidadEventosControlHoras.TiposPersistidos acoplaria el guardrail al mismo artefacto que
    // IdentidadEventosControlHorasTests ya verifica, y la asercion pasaria en verde aunque la lista
    // quedara vacia.
    [Fact]
    public async Task AgregarServiciosControlHoras_RegistraLosTiposDeEventoPersistidos_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertEventosPersistidosRegistrados(
            [typeof(MarcacionRegistrada), typeof(MarcacionAdicionada), typeof(TurnoDiarioAsignado)]);
    }

    // Issue #279 CA-1: el validator del comando no se registra a mano -- lo descubre
    // AddValidatorsFromAssemblyContaining<IControlHorasAssemblyMarker>() por escaneo del ensamblado.
    // RequestValidator es fail-open: si no encuentra un IValidator<T> deja pasar el comando sin
    // validar (ver RequestValidator, "if (validator is null) return (comando, null)"), asi que un
    // validator movido de ensamblado, renombrado a no-publico o un cambio del marker desactivarian la
    // validacion del borde EN SILENCIO -- con el endpoint anonimo y los eventos inmutables detras.
    // Ningun test del validator detecta eso: todos lo instancian directamente.
    [Fact]
    public async Task AgregarServiciosControlHoras_DescubreElValidatorDeRegistrarMarcacion_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var validator = scope.ServiceProvider.GetService<IValidator<RegistrarMarcacion>>();

        validator.Should().BeOfType<RegistrarMarcacionValidator>();
    }

    // Issue #277 CA-5/CA-7/CA-8: registrar el tipo solo sirve si el alias sigue siendo el que las
    // filas ya escritas llevan en su columna "type". AliasEventosControlHorasTests lo congela sobre
    // un StoreOptions standalone; esta guarda lo congela sobre el store del contenedor, el unico
    // lugar donde un MapEventType o un EventNamingStyle agregados al wiring podrian cambiarlo.
    [Fact]
    public async Task AgregarServiciosControlHoras_DerivaElAliasDeEventoDelNombreDeClase_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertAliasDeEventosPersistidos(new Dictionary<Type, string>
        {
            [typeof(MarcacionRegistrada)] = "marcacion_registrada",
            [typeof(MarcacionAdicionada)] = "marcacion_adicionada",
            [typeof(TurnoDiarioAsignado)] = "turno_diario_asignado"
        });
    }

    // Issue #328 CA-4: test de composicion de una Function GET, hermano de MEF-ADR-0029 -- misma
    // idea que las guardas de arriba (grafo de DI real, sin infra desplegada), pero sobre un
    // FunctionEndpoint en vez de un router de Wolverine.
    //
    // Ninguna Function de este ensamblado se registra explicitamente en el contenedor
    // (RegistrarMarcacionFunction, AdicionarMarcacionCuandoRegistroDeMarcacionCreado,
    // AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction: ninguna lleva un
    // services.AddScoped<FunctionEndpoint>() explicito) -- el host de Azure Functions isolated
    // worker las activa por tipo, resolviendo su constructor contra el mismo ServiceProvider que
    // arma Program.cs. ActivatorUtilities.CreateInstance reproduce esa misma activacion sin
    // levantar el host real (Alt 1 de MEF-ADR-0029: no existe un WebApplicationFactory para
    // Functions isolated worker).
    //
    // Se prueba solo la RESOLUCION de IDocumentStore/ITenantResolver por constructor -- no el
    // comportamiento de Run (parseo de empleadoId/fecha con 400 explicito, session.LoadAsync y el
    // 200/404, CA-4), que es responsabilidad de projection-implementer y del smoke test (CA-8), no
    // de este guardrail de wiring. Por eso este test queda en verde tan pronto exista el
    // FunctionEndpoint stub con el constructor correcto -- no es la guarda que fuerza el rojo de
    // este issue (esa la dan los unit tests de la proyeccion y el config-test del worker).
    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeObtenerTurnoVigente_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ObtenerTurnoVigenteEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Issue #328, mismo par que el issue #294 tuvo que cerrar en su momento para el read model
    // anterior (retirado por #323): TurnoVigenteProjection esta registrada en el worker (ver
    // ConfiguracionMartenProjectionsTests.ConfigurarControlHoras_RegistraTurnoVigenteProjection
    // ComoAsync), asi que Marten le aplica ProjectionDocumentPolicy alla (UseNumericRevisions =
    // true, mt_version bigint) -- este Function App NO puede registrar esa proyeccion (vive en el
    // ensamblado del worker, referenciarla violaria CA-ADR-0029) asi que, sin declarar la misma
    // forma explicitamente con Schema.For<TurnoVigente>().UseNumericRevisions(true), esperaria
    // mt_version uuid sobre la MISMA tabla fisica y las dos lecturas convergerian en un "alter
    // column" incompatible (42804), el mismo 500 permanente que produjo el deploy de #290.
    //
    // Oraculo literal, espejo del que ConfiguracionMartenProjectionsTests
    // .ConfigurarControlHoras_MaterializaTurnoVigenteConRevisionNumerica congela desde el worker.
    [Fact]
    public async Task AgregarServiciosControlHoras_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaTurnoVigente()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(TurnoVigente));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Issue #328, mitad write-side del par 2 de compatibilidad write-side/read-side (MEF-ADR-0034
    // seccion 6), heredada de la guarda equivalente que #289 dejo para el read model anterior: este
    // Function App lee TurnoVigente con session.LoadAsync sin registrar la proyeccion, y el worker
    // la materializa en otro proceso. Tabla, tenancy e IdMember tienen que converger o el GET
    // devuelve 404 para siempre con el daemon funcionando.
    //
    // Anadida en la revision de #328: los tres valores los resuelve Marten por convencion, pero este
    // lado ya declara un Schema.For<TurnoVigente>() propio (la linea de UseNumericRevisions del test
    // de arriba) -- justo el tipo de declaracion por documento que puede desviar la tabla o la
    // tenancy de un solo lado. Oraculo literal, espejo del que ConfiguracionMartenProjectionsTests
    // .ConfigurarControlHoras_MaterializaTurnoVigenteSobreLaTablaQueConsultaElWriteSide congela desde
    // el worker.
    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveTurnoVigenteSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(TurnoVigente));

        mapping.TableName.QualifiedName.Should().Be("control_horas.mt_doc_turnovigente");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(TurnoVigente.Id));
    }

    // Issue #329: test de composicion de la Function GET de listado sobre TurnoVigente (#328),
    // hermano del de ObtenerTurnoVigente (#328 CA-4, arriba) y de MEF-ADR-0029. ActivatorUtilities
    // .CreateInstance reproduce la activacion por tipo que hace el host de Azure Functions isolated
    // worker, sin levantar el host real (Alt 1 de MEF-ADR-0029).
    //
    // Se prueba solo la RESOLUCION de IDocumentStore/ITenantResolver por constructor -- no el
    // comportamiento de Run (parseo de desde/hasta/empleadoId, recorte de rango, session.Query y
    // mapeo al envelope de respuesta), que es responsabilidad de projection-implementer y del smoke
    // test, no de este guardrail de wiring. Este issue no crea proyeccion nueva ni toca el seam del
    // worker (issue #329, "Necesidad de lectura"): esta es la UNICA capa read-side declarada para
    // el, junto con la ausencia deliberada de config-test/unit tests de proyeccion (carve-out de
    // coverage de Functions GET, MEF-ADR-0035/issue #371) -- por eso este test queda en verde tan
    // pronto exista el FunctionEndpoint stub con el constructor correcto, igual que su hermano de
    // ObtenerTurnoVigente arriba.
    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeListarTurnosVigentes_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarTurnosVigentesEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // --- Sampler efectivo del TracerProvider (issue #308 CA-2, CA-3) ---
    //
    // Hallazgo 1 del issue #308: UseAzureMonitorExporter llama SetSampler internamente
    // (Azure.Monitor.OpenTelemetry.Exporter 1.8.1) con RateLimitedSampler porque
    // AzureMonitorExporterOptions.TracesPerSecond tiene default 5.0. Escrito en el orden actual de
    // este seam (SetSampler ANTES de UseAzureMonitorExporter), ese SetSampler interno pisa al
    // ParentBasedSampler{TraceIdRatioBasedSampler{ratio}} que CA-ADR-0009 Capa 2 describe -- nunca
    // llega a instalarse. La correccion es de ORDEN (un segundo .WithTracing(...) despues de
    // .UseAzureMonitorExporter()), asi que el guardrail verifica el sampler EFECTIVO resuelto del
    // contenedor, no que el codigo "llame a SetSampler" (eso ya lo hacia el seam roto).
    //
    // A diferencia de Projections (que envuelve el sampler de ratio con
    // SamplerQueDescartaPollingDelDaemon porque el worker corre el daemon HotCold de Marten),
    // ControlHoras no corre ningun daemon -- MEF-ADR-0018 Rule of Three: el filtro tiene un solo
    // consumidor real (el worker) y no se generaliza aqui. El sampler efectivo esperado es
    // directamente el ParentBasedSampler configurado por el proyecto, sin wrapper.
    [Fact]
    public void AgregarServiciosControlHoras_ResuelveElSamplerConfiguradoPorElProyecto_EnVezDeRateLimitedSampler()
    {
        // TracerProvider se registra Singleton (default de OpenTelemetry): no requiere un scope
        // Wolverine (IAsyncDisposable) para resolverse, a diferencia de ICommandRouter/
        // IPrivateEventRouter arriba -- Dispose() sincrono del ServiceProvider raiz basta.
        using var provider = ComponerServiceProvider();
        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        var samplerEfectivo = ObtenerSamplerEfectivo(tracerProvider);

        // Oraculo por nombre completo, no por referencia al tipo (es internal en otro ensamblado:
        // Azure.Monitor.OpenTelemetry.Exporter.Internals.RateLimitedSampler no se puede nombrar
        // desde este proyecto). Verificado en runtime (issue #308) que este es exactamente el tipo
        // que gana hoy con el wiring actual.
        samplerEfectivo.GetType().FullName.Should().NotBe(
            "Azure.Monitor.OpenTelemetry.Exporter.Internals.RateLimitedSampler");
        samplerEfectivo.Should().BeOfType<ParentBasedSampler>();
    }

    // CA-3: complementa (no reemplaza) el parsing inline del ratio que ya vive en
    // ComposicionServicios.cs -- verifica que el ratio efectivamente llega al sampler compuesto que
    // el contenedor resuelve, no solo que se calcula correctamente. Determinista via
    // Sampler.Description (propiedad publica: "ParentBased{TraceIdRatioBasedSampler{F6}}").
    [Fact]
    public void AgregarServiciosControlHoras_PropagaElRatioDeSamplingConfigurado_AlSamplerEfectivo()
    {
        ConVariableDeEntorno(VariableRatioSampling, "0.5", () =>
        {
            using var provider = ComponerServiceProvider();
            var tracerProvider = provider.GetRequiredService<TracerProvider>();

            var samplerEfectivo = ObtenerSamplerEfectivo(tracerProvider);

            samplerEfectivo.Description.Should().Contain(FormatearRatio(0.5));
        });
    }

    // La otra mitad de CA-3, y la que mas importa hoy: TELEMETRY_SAMPLING_RATIO no esta puesta en
    // ningun recurso desplegado (medido en el issue #308), asi que el camino que efectivamente corre
    // en dev es el del DEFAULT. Sin este guardrail, el sampler efectivo podria quedar con un ratio
    // distinto al declarado y solo se veria como un volumen de ingestion inesperado -- el mismo modo
    // de falla silenciosa que este issue corrige.
    [Fact]
    public void AgregarServiciosControlHoras_PropagaElRatioPorDefecto_AlSamplerEfectivo_CuandoLaVariableEstaAusente()
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

    // --- Issue #309: apagar la recoleccion de metricas de durabilidad de Wolverine (CA-1, CA-3) ---
    //
    // PersistenceMetrics.StartPolling (Wolverine.RDBMS.DurabilityAgent.StartAsync) es un
    // PeriodicTimer(DurabilitySettings.UpdateMetricsPeriod, default 5s) que llama
    // store.Admin.FetchCountsAsync() -- de ahi salen las cuatro consultas Postgres medidas en dev
    // (123.050 spans/24h, 85/min): el "group by status" de wolverine_incoming_envelopes mas dos
    // estimateTableCount (pg_class) con su fallback select count(*). Nadie consume esas metricas
    // hoy (sin dashboard ni alerta) y CheckHealthAsync llama FetchCountsAsync por su cuenta, asi
    // que el health check no depende de este polling.
    //
    // Se resuelve WolverineOptions del CONTENEDOR REAL (no un DurabilitySettings construido a
    // mano) porque el gotcha de wiring verificado en el issue es de ORDEN: dentro de
    // AgregarWolverineParaComandosServerless (Cosmos.EventSourcing.CritterStack 2.3.1) el callback
    // del consumidor corre PRIMERO y options.Durability.Mode = Solo se asigna DESPUES, pisando
    // cualquier intento de tocar Mode desde el callback -- pero NO pisa DurabilityMetricsEnabled.
    // Un test que solo construyera un DurabilitySettings a mano nunca detectaria si un futuro
    // upgrade del paquete empieza a pisar tambien esa bandera; resolver el WolverineOptions
    // efectivo del grafo de DI si lo detecta.
    //
    // El provider se libera con await using porque WolverineOptions se registra Singleton via
    // factory-lambda -- el contenedor lo trackea para disposal -- y solo implementa IAsyncDisposable:
    // el Dispose() sincrono del provider raiz lanzaria InvalidOperationException al encontrarlo entre
    // sus disposables. Mismo motivo por el que los tests de ICommandRouter/IPrivateEventRouter usan
    // await using, y a diferencia de los de TracerProvider (singleton IDisposable, que si tolera el
    // using sincrono).
    [Fact]
    public async Task AgregarServiciosControlHoras_ApagaLaRecoleccionDeMetricasDeDurabilidad_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityMetricsEnabled.Should().BeFalse();
    }

    // CA-3: este issue apaga la recoleccion de METRICAS de cola, no la durabilidad real -- recovery,
    // scheduled jobs y dead letters (procesados por el mismo DurabilityAgent) siguen activos. Fijar
    // Mode en el mismo test que DurabilityMetricsEnabled evita que una futura correccion de CA-1 se
    // resuelva "apagando" la durabilidad completa (p.ej. cambiando Mode) en vez de solo la bandera de
    // metricas -- Mode sigue siendo Solo, el valor que AgregarWolverineParaComandosServerless fija
    // incondicionalmente DESPUES del callback del consumidor.
    [Fact]
    public async Task AgregarServiciosControlHoras_ConservaLaDurabilidadReal_CuandoApagaLasMetricasDeCola()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityAgentEnabled.Should().BeTrue();
        opciones.Durability.Mode.Should().Be(DurabilityMode.Solo);
    }
}
