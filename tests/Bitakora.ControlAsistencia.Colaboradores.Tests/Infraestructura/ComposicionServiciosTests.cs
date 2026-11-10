// Compone el contenedor DI real (el mismo AgregarServiciosColaboradores que invoca Program.cs) con
// connection strings dummy: un registro faltante (p. ej. ITenantContext) compila y pasa los tests
// unitarios, y solo aparece tras el deploy como HTTP 500 en toda funcion.
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
using Bitakora.ControlAsistencia.ReadModels;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.EventSourcing.Abstractions.Commands;
using FluentValidation;
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*: vive en JasperFx.MultiTenancy)
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Wolverine;
using ObtenerFichaColaboradorEndpoint = Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador.FunctionEndpoint;
using ListarFichasColaboradorEndpoint = Bitakora.ControlAsistencia.Colaboradores.ListarFichasColaborador.FunctionEndpoint;
using ListarCategoriasDeEtiquetasEndpoint = Bitakora.ControlAsistencia.Colaboradores.ListarCategoriasDeEtiquetas.FunctionEndpoint;
using ListarDirectorioColaboradoresEndpoint = Bitakora.ControlAsistencia.Colaboradores.ListarDirectorioColaboradores.FunctionEndpoint;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.Infraestructura;

public class ComposicionServiciosTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    // Variable y ratio por defecto repetidos como literal de ComposicionServicios.cs: el dominio no
    // declara InternalsVisibleTo hacia sus tests.
    private const string VariableRatioSampling = "TELEMETRY_SAMPLING_RATIO";
    // El seam nunca lee esta variable (el secreto no pasa por el codigo del dominio): se nombra solo para
    // simular su presencia en el guardrail del PostConfigure.
    private const string VariableConnectionStringAppInsights = "APPLICATIONINSIGHTS_CONNECTION_STRING";

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

    // Restaura el valor original al terminar; null elimina la variable.
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

    // Lee por reflection la propiedad interna Sampler de TracerProviderSdk: asi no hace falta muestrear
    // actividades reales contra un ratio fraccionario.
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

    [Fact]
    public void AgregarServiciosColaboradores_ResuelveElSamplerConfiguradoPorElProyecto_EnVezDeRateLimitedSampler()
    {
        // TracerProvider es Singleton: basta el Dispose() sincrono del provider raiz, sin scope.
        using var provider = ComponerServiceProvider();
        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        var samplerEfectivo = ObtenerSamplerEfectivo(tracerProvider);

        // Por nombre completo: RateLimitedSampler es internal en Azure.Monitor.OpenTelemetry.Exporter.
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

    [Fact]
    public async Task AgregarServiciosColaboradores_ApagaLaRecoleccionDeMetricasDeDurabilidad_CuandoElContenedorEstaCompuesto()
    {
        // await using: WolverineOptions es Singleton por factory-lambda y solo implementa IAsyncDisposable.
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityMetricsEnabled.Should().BeFalse();
    }

    // Se apagan las metricas de cola, no la durabilidad real (recovery, scheduled jobs, dead letters).
    // Mode sigue Solo: AgregarWolverineParaComandosServerless lo fija despues del callback del consumidor.
    [Fact]
    public async Task AgregarServiciosColaboradores_ConservaLaDurabilidadReal_CuandoApagaLasMetricasDeCola()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityAgentEnabled.Should().BeTrue();
        opciones.Durability.Mode.Should().Be(DurabilityMode.Solo);
    }

    // Tipos listados literalmente (oraculo independiente): leerlos de TiposPersistidos pasaria en verde
    // aunque la lista quedara vacia.
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
                typeof(EtiquetaRetirada),
                typeof(SedeAsignada),
                typeof(JornadaAsignada)
            ]);
    }

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
            [typeof(EtiquetaRetirada)] = "etiqueta_retirada",
            [typeof(SedeAsignada)] = "sede_asignada",
            [typeof(JornadaAsignada)] = "jornada_asignada"
        });
    }

    [Fact]
    public async Task AgregarServiciosColaboradores_CongelaElAliasDeJornadaAsignada()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var alias = store.Options.Events.AllKnownEventTypes()
            .Single(e => e.EventType == typeof(JornadaAsignada)).Alias;

        alias.Should().Be("jornada_asignada");
    }

    // Las banderas de metadata comparten el callback ConfigureMarten que registra el resolver de
    // serializacion custom, y los round-trip de *SerializacionTests usan CrearOpcionesMarten() sin pasar
    // por el contenedor: aqui se ejercita el ISerializer del store real, con Identificacion de canario.
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

    // Las Functions no se registran en el contenedor: el host isolated worker las activa por tipo contra
    // el mismo ServiceProvider. ActivatorUtilities.CreateInstance reproduce esa activacion sin host real
    // (no hay WebApplicationFactory para Functions). Solo prueba la resolucion por constructor, no Run.
    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveElEndpointDeObtenerFichaColaborador_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ObtenerFichaColaboradorEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Este Function App lee FichaColaborador sin registrar su proyeccion; el worker la materializa sobre
    // la misma tabla. Marten aplica a todo target de proyeccion revision numerica (mt_version bigint) y
    // deshabilita Version (uuid), incondicionalmente (https://martendb.io/documents/concurrency,
    // "Numeric Revisioned Documents"). Sin declararlo aqui, este store espera uuid sobre la columna
    // bigint, intenta "alter column" en cada request y Postgres responde 42804: GET en 500 permanente.
    // Oraculo literal, espejo de ConfiguracionMartenProjectionsTests
    // .ConfigurarColaboradores_MaterializaFichaColaboradorConRevisionNumerica (sin referencias entre
    // ensamblados, CA-ADR-0029).
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

    // Tabla, tenancy e IdMember deben converger entre el worker y este Function App, o el GET responde
    // 404 para siempre: son dos configuraciones de Marten independientes sobre el mismo schema, y el
    // Schema.For<FichaColaborador>() propio de este lado puede desviarlas. Espejo de
    // ConfiguracionMartenProjectionsTests.ConfigurarColaboradores_MaterializaFichaColaboradorSobreLaTablaQueConsultaElWriteSide.
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

    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveElEndpointDeListarFichasColaborador_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarFichasColaboradorEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveElEndpointDeListarCategoriasDeEtiquetas_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarCategoriasDeEtiquetasEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Cada documento lleva su propio mapping: la guarda de FichaColaborador (gotcha completo arriba) no
    // cubre esta vista. Espejo de ConfiguracionMartenProjectionsTests
    // .ConfigurarColaboradores_MaterializaCategoriaDeEtiquetasConRevisionNumerica.
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

    // Misma convergencia que la de FichaColaborador; aqui el GET devolveria coleccion vacia. Espejo de
    // ConfiguracionMartenProjectionsTests.ConfigurarColaboradores_MaterializaCategoriaDeEtiquetasSobreLaTablaQueConsultaElWriteSide.
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

    // Falla silenciosa: RequestValidator.ValidarAsync deja pasar el body sin validar si no encuentra
    // IValidator<T>, asi que un validator que AddValidatorsFromAssemblyContaining no recoja convierte los
    // 400 de forma en exito, y los unit tests (con IRequestValidator fake) no lo ven.
    [Fact]
    public async Task AgregarServiciosColaboradores_DescubreLosValidatorsDeLosBodiesDeComando_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        scope.ServiceProvider.GetService<IValidator<TerminarVinculacionBody>>()
            .Should().NotBeNull(
                "sin el validator registrado, un body sin FechaEfectiva responderia 204 en vez de 400");
        scope.ServiceProvider.GetService<IValidator<CorregirFechaInicioVinculacionBody>>()
            .Should().NotBeNull(
                "sin el validator registrado, un body sin FechaCorregida responderia 204 en vez de 400");
    }

    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveElEndpointDeReady_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ReadyCheck>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Con EnableTraceBasedLogsSampler en su default (true), el exporter descarta todo LogRecord emitido
    // dentro de un span no muestreado; con ratio fraccionario eso pierde en silencio los LogError que
    // alimentan la alerta exception_spike.
    [Fact]
    public void AgregarServiciosColaboradores_DeshabilitaElSamplerDeLogsBasadoEnTrazas()
    {
        using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<IOptions<AzureMonitorExporterOptions>>().Value;

        opciones.EnableTraceBasedLogsSampler.Should().BeFalse();
    }

    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveElEndpointDeListarDirectorioColaboradores_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarDirectorioColaboradoresEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Tercera vista, mismo gotcha que FichaColaborador (arriba). Espejo de ConfiguracionMartenProjectionsTests
    // .ConfigurarColaboradores_MaterializaDirectorioColaboradorConRevisionNumerica.
    [Fact]
    public async Task AgregarServiciosColaboradores_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaDirectorioColaborador()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(DirectorioColaborador));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Misma convergencia que la de FichaColaborador; aqui el QUERY devolveria coleccion vacia. Espejo de
    // ConfiguracionMartenProjectionsTests.ConfigurarColaboradores_MaterializaDirectorioColaboradorSobreLaTablaQueConsultaElWriteSide.
    [Fact]
    public async Task AgregarServiciosColaboradores_ResuelveDirectorioColaboradorSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(DirectorioColaborador));

        mapping.TableName.QualifiedName.Should().Be("colaboradores.mt_doc_directoriocolaborador");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(DirectorioColaborador.Id));
    }

    // El seam rellena ConnectionString con un valor inerte cuando esta vacia (el reader de metricas la
    // exige al arrancar en frio, con la Key Vault reference aun sin resolver). Si pisara una real, trazas
    // y logs se exportarian en silencio a un endpoint inexistente; nada en el codigo garantiza el orden
    // Configure -> PostConfigure ante un upgrade del exporter: este test si.
    [Fact]
    public void AgregarServiciosColaboradores_ConservaLaConnectionStringReal_CuandoLaVariableDeEntornoEstaPresente()
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

    [Fact]
    public async Task AgregarServiciosColaboradores_DeclaraRevisionNumericaEnTodoDocumentoMarcadoPorProyeccion()
    {
        var marcados = typeof(MaterializadoPorProyeccionAttribute).Assembly.GetTypes()
            .Where(t => t.Namespace == "Bitakora.ControlAsistencia.ReadModels.Colaboradores"
                && t.IsDefined(typeof(MaterializadoPorProyeccionAttribute), inherit: false))
            .ToList();
        marcados.Should().NotBeEmpty();

        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IDocumentStore>().Options;

        foreach (var tipo in marcados)
        {
            var mapping = options.FindOrResolveDocumentType(tipo);

            mapping.Metadata.Revision.Enabled.Should().BeTrue(
                "falta declarar Schema.For<{0}>().UseNumericRevisions(true) en ComposicionServicios", tipo.Name);
            mapping.Metadata.Revision.Type.Should().Be("bigint", "revision de {0}", tipo.Name);
            mapping.Metadata.Version.Enabled.Should().BeFalse("version de {0}", tipo.Name);
        }
    }
}
