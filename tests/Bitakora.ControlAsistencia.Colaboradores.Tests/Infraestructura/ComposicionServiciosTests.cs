// Issue #360 (replica del patron fijado en issue #221 para Programacion/ControlHoras): guardrail
// de CI que compone el contenedor DI real del dominio (el mismo metodo que invoca Program.cs, ver
// ComposicionServicios.AgregarServiciosColaboradores) y valida que el grafo completo es resoluble,
// sin infra desplegada (connection strings dummy).
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
using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.EventSourcing.Abstractions.Commands;
using FluentValidation;
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*: vive en JasperFx.MultiTenancy)
using Marten;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Wolverine;
using ObtenerFichaColaboradorEndpoint = Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador.FunctionEndpoint;
using ListarFichasColaboradorEndpoint = Bitakora.ControlAsistencia.Colaboradores.ListarFichasColaborador.FunctionEndpoint;
using ListarCategoriasDeEtiquetasEndpoint = Bitakora.ControlAsistencia.Colaboradores.ListarCategoriasDeEtiquetas.FunctionEndpoint;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.Infraestructura;

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
    private const double RatioSamplingPorDefecto = 0.2;

    private static ServiceProvider ComponerServiceProvider()
    {
        var services = new ServiceCollection();

        services.AgregarServiciosColaboradores(
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
    public void AgregarServiciosColaboradores_ComponeElGrafoCompleto_CuandoLasConnectionStringsSonDummy()
    {
        var act = () => ComponerServiceProvider().Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveICommandRouter_CuandoElContenedorEstaCompuesto()
    {
        // Wolverine registra dependencias scoped que solo implementan IAsyncDisposable
        // (Wolverine.Persistence.MessageStoreCollection); el scope se libera con DisposeAsync.
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ICommandRouter>();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosColaboradores_HabilitaColumnasDeMetadataDeEvento_CuandoElContenedorEstaCompuesto()
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
    public void AgregarServiciosColaboradores_ResuelveElSamplerConfiguradoPorElProyecto_EnVezDeRateLimitedSampler()
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
    public void AgregarServiciosColaboradores_PropagaElRatioDeSamplingConfigurado_AlSamplerEfectivo()
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
    public void AgregarServiciosColaboradores_PropagaElRatioPorDefecto_AlSamplerEfectivo_CuandoLaVariableEstaAusente()
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
    public async Task AgregarServiciosColaboradores_ApagaLaRecoleccionDeMetricasDeDurabilidad_CuandoElContenedorEstaCompuesto()
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
    public async Task AgregarServiciosColaboradores_ConservaLaDurabilidadReal_CuandoApagaLasMetricasDeCola()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityAgentEnabled.Should().BeTrue();
        opciones.Durability.Mode.Should().Be(DurabilityMode.Solo);
    }

    // Issue #330 (patron replicado de ControlHoras/Programacion #277 CA-2/CA-4): ColaboradorRegistrado
    // y VinculacionIniciada nacen con este issue en Colaboradores.DomainEvents sin registrar su tipo
    // en el EventGraph. Toda lectura quedaria dependiendo del fallback por mt_dotnet_type en vez de
    // resolver por alias (columna "type" de mt_events). Este guardrail detecta el olvido sobre el
    // store real que compone el contenedor, sin Postgres.
    //
    // Los tipos esperados se listan literalmente (oraculo independiente, MEF-ADR-0002): leerlos de
    // IdentidadEventosColaboradores.TiposPersistidos acoplaria el guardrail al mismo artefacto que
    // AliasEventosColaboradoresTests ya verifica, y la asercion pasaria en verde aunque la lista
    // quedara vacia.
    // Issue #349: agrega VinculacionTerminada (segundo evento persistido de la vinculacion) a la
    // lista esperada -- mismo criterio, mismo guardrail.
    // Issue #351: agrega NombresCorregidos (cuarto evento persistido, corregir nombres) a la lista
    // esperada.
    // Rojo esperado (fase roja, issue #351): TiposPersistidos todavia no incluye NombresCorregidos.
    // Issue #352: agrega FechaInicioVinculacionCorregida (quinto evento persistido, corregir la
    // fecha de inicio de la ultima vinculacion) a la lista esperada.
    // Rojo esperado (fase roja, issue #352): TiposPersistidos todavia no incluye
    // FechaInicioVinculacionCorregida.
    // Issue #354: agrega TerminacionAnulada (sexto evento persistido, anular la terminacion de la
    // ultima vinculacion) a la lista esperada.
    // Issue #355: agrega EtiquetaAsignada y EtiquetaRetirada (septimo y octavo eventos persistidos,
    // asignar/retirar una etiqueta dinamica) a la lista esperada.
    // Rojo esperado (fase roja, issue #355): TiposPersistidos todavia no incluye ninguno de los
    // dos.
    [Fact]
    public async Task AgregarServiciosColaboradores_RegistraLosTiposDeEventoPersistidos_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertEventosPersistidosRegistrados(
            [
                typeof(ColaboradorRegistrado),
                typeof(VinculacionIniciada),
                typeof(VinculacionTerminada),
                typeof(NombresCorregidos),
                typeof(FechaInicioVinculacionCorregida),
                typeof(TerminacionAnulada),
                typeof(EtiquetaAsignada),
                typeof(EtiquetaRetirada)
            ]);
    }

    // Issue #330: registrar el tipo solo sirve si el alias sigue siendo el que las filas ya
    // escritas llevan en su columna "type". AliasEventosColaboradoresTests lo congela sobre un
    // StoreOptions standalone; esta guarda lo congela sobre el store del contenedor, el unico lugar
    // donde un MapEventType o un EventNamingStyle agregados al wiring podrian cambiarlo.
    // Issue #349: agrega VinculacionTerminada -> "vinculacion_terminada" al diccionario esperado.
    // Issue #351: agrega NombresCorregidos -> "nombres_corregidos" al diccionario esperado.
    // Rojo esperado (fase roja, issue #351): NombresCorregidos no aparece en AllKnownEventTypes().
    // Issue #352: agrega FechaInicioVinculacionCorregida -> "fecha_inicio_vinculacion_corregida"
    // al diccionario esperado.
    // Rojo esperado (fase roja, issue #352): FechaInicioVinculacionCorregida no aparece en
    // AllKnownEventTypes().
    // Issue #354: agrega TerminacionAnulada -> "terminacion_anulada" al diccionario esperado.
    // Issue #355: agrega EtiquetaAsignada -> "etiqueta_asignada" y EtiquetaRetirada ->
    // "etiqueta_retirada" al diccionario esperado.
    [Fact]
    public async Task AgregarServiciosColaboradores_DerivaElAliasDeEventoDelNombreDeClase_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertAliasDeEventosPersistidos(new Dictionary<Type, string>
        {
            [typeof(ColaboradorRegistrado)] = "colaborador_registrado",
            [typeof(VinculacionIniciada)] = "vinculacion_iniciada",
            [typeof(VinculacionTerminada)] = "vinculacion_terminada",
            [typeof(NombresCorregidos)] = "nombres_corregidos",
            [typeof(FechaInicioVinculacionCorregida)] = "fecha_inicio_vinculacion_corregida",
            [typeof(TerminacionAnulada)] = "terminacion_anulada",
            [typeof(EtiquetaAsignada)] = "etiqueta_asignada",
            [typeof(EtiquetaRetirada)] = "etiqueta_retirada"
        });
    }

    // Issue #330 (patron replicado de ControlHoras #232 CA-5): las tres banderas de metadata
    // (arriba) comparten el mismo callback ConfigureMarten que debe registrar el resolver de
    // serializacion custom -- un edit futuro de ese bloque puede tumbar la serializacion sin que el
    // test de metadata se ponga rojo. Los round-trip de ColaboradorRegistradoSerializacionTests NO
    // cubren este riesgo: usan ConfiguracionSerializacionColaboradores.CrearOpcionesMarten() -- una
    // ruta paralela que no atraviesa el contenedor. Este test ejercita el ISerializer que el store
    // realmente compuso, usando Identificacion (VO real y completo de #348) como canario.
    // Rojo esperado (fase roja, issue #330): ComposicionServicios.cs todavia no invoca
    // ConfiguracionSerializacionColaboradores.ConfigurarResolver dentro de ConfigureMarten.
    [Fact]
    public async Task AgregarServiciosColaboradores_ConservaLaSerializacionCustom_CuandoTambienHabilitaMetadataDeEvento()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var serializador = scope.ServiceProvider.GetRequiredService<IDocumentStore>().Options.Serializer();
        var original = Identificacion.Crear(TipoIdentificacion.CC, "79543210");

        using var json = new MemoryStream(Encoding.UTF8.GetBytes(serializador.ToJson(original)));
        var restaurado = serializador.FromJson<Identificacion>(json);

        restaurado.Should().Be(original);
        restaurado.ToString().Should().Be(original.ToString());
    }

    // Issue #356 CA-6: test de composicion de una Function GET, hermano de MEF-ADR-0029 -- misma
    // idea que las guardas de arriba (grafo de DI real, sin infra desplegada), pero sobre un
    // FunctionEndpoint en vez de un router de Wolverine. Precedente: ObtenerTurnoVigente/
    // ListarTurnosVigentes en ControlHoras.Tests (issue #328/#329).
    //
    // Ninguna Function de este ensamblado se registra explicitamente en el contenedor -- el host
    // de Azure Functions isolated worker las activa por tipo, resolviendo su constructor contra el
    // mismo ServiceProvider que arma Program.cs. ActivatorUtilities.CreateInstance reproduce esa
    // misma activacion sin levantar el host real (Alt 1 de MEF-ADR-0029: no existe un
    // WebApplicationFactory para Functions isolated worker).
    //
    // Se prueba solo la RESOLUCION de IDocumentStore/ITenantResolver por constructor -- no el
    // comportamiento de Run (issue #386: parseo del {id} de ruta con 400 explicito,
    // session.LoadAsync y el 200/404, la traduccion centinela -> vacio de CA-6), que es
    // responsabilidad del endpoint (parcialmente unit-testeado, ver ObtenerFichaColaborador/
    // FunctionEndpointTests.cs) y del smoke test, no de este guardrail de wiring.
    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveElEndpointDeObtenerFichaColaborador_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ObtenerFichaColaboradorEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Issue #356, mitad write-side del par 2 de compatibilidad write-side/read-side (MEF-ADR-0034
    // seccion 6). Heredada de la guarda que #328 dejo para TurnoVigente en ControlHoras.Tests, por
    // el incidente real del issue #294: este Function App LEE FichaColaborador con
    // session.LoadAsync (ObtenerFichaColaborador) sin registrar la proyeccion, mientras el worker
    // la MATERIALIZA en otro proceso sobre la misma tabla fisica.
    //
    // Marten aplica ProjectionDocumentPolicy a todo documento que sea target de una proyeccion
    // registrada en ese store: UseNumericRevisions = true, Metadata.Revision (mt_version bigint)
    // habilitada y Metadata.Version (mt_version uuid) DESHABILITADA -- incondicional, sin opt-in ni
    // dependencia de IRevisioned (https://martendb.io/documents/concurrency, "Numeric Revisioned
    // Documents"). Sin la declaracion explicita del lado lectura, este store espera mt_version uuid
    // sobre la tabla que el worker creo como bigint, Marten intenta "alter column" en CADA request
    // y Postgres responde 42804: GET en 500 permanente con el daemon funcionando.
    //
    // Oraculo literal, espejo del que ConfiguracionMartenProjectionsTests
    // .ConfigurarColaboradores_MaterializaFichaColaboradorConRevisionNumerica congela desde el
    // worker, sin que ningun ensamblado referencie al otro (CA-ADR-0029).
    [Fact]
    public async Task AgregarServiciosColaboradores_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaFichaColaborador()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaColaborador));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Issue #356, segunda dimension del mismo par 2 (precedente #328 sobre TurnoVigente): tabla,
    // tenancy e IdMember tienen que converger entre el worker que materializa y este Function App
    // que consulta, o el GET responde 404 para siempre con el daemon funcionando. Ningun compilador
    // lo garantiza -- son dos configuraciones de Marten independientes sobre el mismo schema.
    //
    // Los tres valores los resuelve Marten por convencion, pero este lado ya declara un
    // Schema.For<FichaColaborador>() propio (la linea de UseNumericRevisions del test de arriba) --
    // justo el tipo de declaracion por documento que puede desviar la tabla o la tenancy de un solo
    // lado. Oraculo literal, espejo del que ConfiguracionMartenProjectionsTests
    // .ConfigurarColaboradores_MaterializaFichaColaboradorSobreLaTablaQueConsultaElWriteSide congela
    // desde el worker.
    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveFichaColaboradorSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaColaborador));

        mapping.TableName.QualifiedName.Should().Be("colaboradores.mt_doc_fichacolaborador");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(FichaColaborador.Id));
    }

    // Issue #373 CA-4 (segunda mitad del desglose de #356): test de composicion de la SEGUNDA
    // Function de lectura del dominio, hermano del que #356 dejo para ObtenerFichaColaborador --
    // misma idea (MEF-ADR-0029/ActivatorUtilities.CreateInstance, sin host real), pero ahora sobre
    // un endpoint QUERY (MEF-ADR-0042) en vez de GET. Ninguna proyeccion ni read model nuevos: este
    // issue consulta la MISMA vista materializada FichaColaborador via (a') (session.Query, en vez
    // de LoadAsync por id).
    //
    // Se prueba solo la RESOLUCION de IDocumentStore/ITenantResolver por constructor -- no el
    // comportamiento de Run (415/400/422, filtro AND por etiquetas, paginacion keyset), que es
    // responsabilidad del endpoint y cubre FunctionEndpointTests.cs (validacion) y el smoke test
    // contra dev (CA-6, camino feliz + Marten real).
    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveElEndpointDeListarFichasColaborador_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarFichasColaboradorEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Issue #357: test de composicion de la Function GET del catalogo CategoriaDeEtiquetas, mismo
    // patron que #356 dejo para ObtenerFichaColaborador (MEF-ADR-0029: ActivatorUtilities
    // .CreateInstance, sin host real -- no existe un WebApplicationFactory para Functions isolated
    // worker). El endpoint recibe IDocumentStore/ITenantResolver por constructor, ya registrados por
    // AgregarServiciosColaboradores desde el issue #360 -- no hace falta ningun registro nuevo para
    // que este guardrail de wiring resuelva.
    //
    // Se prueba solo la RESOLUCION de dependencias por constructor -- no el comportamiento de Run
    // (200 con la coleccion, incluso vacia -- CA-6), que es responsabilidad del endpoint y del smoke
    // test contra dev.
    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveElEndpointDeListarCategoriasDeEtiquetas_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarCategoriasDeEtiquetasEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Issue #357, mitad write-side del par 2 de compatibilidad write-side/read-side (MEF-ADR-0034
    // seccion 6) para la SEGUNDA vista materializada del dominio -- hermano exacto del guardrail
    // que #356 dejo para FichaColaborador, cuyo comentario documenta el gotcha completo de
    // "Numeric Revisioned Documents" y el incidente de dev del issue #294.
    //
    // Ninguna de las dos guardas de FichaColaborador cubre a CategoriaDeEtiquetas: cada documento
    // lleva su propio mapping, asi que la declaracion explicita del lado LECTURA es por vista, no
    // por store. Sin ella, este Function App esperaria mt_version uuid sobre la tabla que el worker
    // crea como bigint y ListarCategoriasDeEtiquetas responderia 500 permanente (42804 por
    // request), con el daemon funcionando.
    //
    // Oraculo literal, espejo del que ConfiguracionMartenProjectionsTests
    // .ConfigurarColaboradores_MaterializaCategoriaDeEtiquetasConRevisionNumerica congela desde el
    // worker, sin que ningun ensamblado referencie al otro (CA-ADR-0029).
    [Fact]
    public async Task AgregarServiciosColaboradores_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaCategoriaDeEtiquetas()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(CategoriaDeEtiquetas));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Issue #357, segunda dimension del mismo par 2 sobre CategoriaDeEtiquetas (precedente #356
    // sobre FichaColaborador): tabla, tenancy e IdMember tienen que converger entre el worker que
    // materializa y este Function App que consulta, o el GET devuelve coleccion vacia para siempre
    // con el daemon funcionando. Ningun compilador lo garantiza -- son dos configuraciones de
    // Marten independientes sobre el mismo schema.
    //
    // Oraculo literal, espejo del que ConfiguracionMartenProjectionsTests
    // .ConfigurarColaboradores_MaterializaCategoriaDeEtiquetasSobreLaTablaQueConsultaElWriteSide
    // congela desde el worker.
    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveCategoriaDeEtiquetasSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(CategoriaDeEtiquetas));

        mapping.TableName.QualifiedName.Should().Be("colaboradores.mt_doc_categoriadeetiquetas");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(CategoriaDeEtiquetas.Id));
    }

    // Issue #379 (agregado en la revision): guardrail del descubrimiento de los validators de body.
    //
    // El modo de falla que atrapa es SILENCIOSO por construccion: RequestValidator.ValidarAsync
    // resuelve IValidator<T> del contenedor y, si no lo encuentra, DEJA PASAR el body sin validar
    // (retorna (comando, null)) en vez de fallar. Un validator que el escaneo
    // AddValidatorsFromAssemblyContaining no recoja convierte todos los 400 de forma de ese comando
    // en 202 -- compila, pasa todos los tests unitarios (que inyectan un IRequestValidator fake) y
    // solo aflora contra dev.
    //
    // El issue #379 movio estos validators de CommandHandler/{Comando}Validator (sobre el comando
    // interno completo) a {Comando}BodyValidator en la raiz del feature folder (sobre el body
    // reducido), y elimino los tres viejos: exactamente el tipo de cambio de ubicacion/tipo generico
    // que este guardrail vigila. Se listan los dos comandos del issue con body; AnularTerminacion no
    // aparece porque quedo SIN body (sus tres campos viajan en la ruta), asi que no tiene validator
    // que descubrir.
    [Fact]
    public async Task AgregarServiciosColaboradores_DescubreLosValidatorsDeLosBodiesDeComando_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        scope.ServiceProvider.GetService<IValidator<TerminarVinculacionBody>>()
            .Should().NotBeNull(
                "sin el validator registrado, un body sin FechaEfectiva responderia 202 en vez de 400");
        scope.ServiceProvider.GetService<IValidator<CorregirFechaInicioVinculacionBody>>()
            .Should().NotBeNull(
                "sin el validator registrado, un body sin FechaCorregida responderia 202 en vez de 400");
    }
}
