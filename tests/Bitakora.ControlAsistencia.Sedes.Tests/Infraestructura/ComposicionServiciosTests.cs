// Guardrail de CI: compone el contenedor DI real del dominio (el mismo metodo que invoca Program.cs)
// y valida que el grafo completo es resoluble, sin infra desplegada. Atrapa, por ejemplo, un
// ITenantContext sin registrar: compila, pasa los unit tests y solo falla despues del deploy (HTTP
// 500 en toda funcion).
//
// Limite conocido: BuildServiceProvider(ValidateOnBuild) no valida registros por factory-lambda
// (Wolverine/Marten registran varios), solo los de tipo mapeado -- de ahi la resolucion explicita
// de ICommandRouter en un scope, ademas del build general.

using System.Globalization;
using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels;
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

    // Repetidos como literal: ComposicionServicios.cs los tiene inline y este dominio no declara
    // InternalsVisibleTo hacia sus tests.
    private const string VariableRatioSampling = "TELEMETRY_SAMPLING_RATIO";
    // El seam nunca la lee (MEF-ADR-0025: el secreto no pasa por el codigo del dominio); aqui solo se
    // simula su presencia.
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

    // Lee la propiedad interna `Sampler` de TracerProviderSdk (no publica en OpenTelemetry.dll) para
    // comparar el sampler instalado de forma determinista.
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

    [Fact]
    public void AgregarServiciosSedes_ResuelveElSamplerConfiguradoPorElProyecto_EnVezDeRateLimitedSampler()
    {
        // TracerProvider es Singleton y no necesita un scope de Wolverine: basta el Dispose() sincrono.
        using var provider = ComponerServiceProvider();
        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        var samplerEfectivo = ObtenerSamplerEfectivo(tracerProvider);

        // Por nombre completo: RateLimitedSampler es internal en el ensamblado del exporter.
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

    // Solo se apagan las METRICAS de cola: recovery, scheduled jobs y dead letters (mismo
    // DurabilityAgent) siguen activos. Mode sigue en Solo, que AgregarWolverineParaComandosServerless
    // fija DESPUES del callback del consumidor.
    [Fact]
    public async Task AgregarServiciosSedes_ConservaLaDurabilidadReal_CuandoApagaLasMetricasDeCola()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityAgentEnabled.Should().BeTrue();
        opciones.Durability.Mode.Should().Be(DurabilityMode.Solo);
    }

    // ReadyCheck depende de IEventStoreReadinessProbe.
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveElEndpointDeReady_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ReadyCheck>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Sin registrar el tipo en el EventGraph, toda lectura dependeria del fallback por mt_dotnet_type
    // en vez del alias. Los tipos se listan literales (MEF-ADR-0002): leerlos de
    // IdentidadEventosSedes.TiposPersistidos haria pasar la asercion aunque la lista quedara vacia.
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

    // Hermano de MEF-ADR-0029 para una Function GET: ActivatorUtilities.CreateInstance reproduce la
    // activacion por tipo del host isolated worker sin levantarlo. Solo prueba la RESOLUCION del
    // constructor, no el comportamiento de Run (lo cubren el unit test de la proyeccion y el smoke).
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveElEndpointDeObtenerFichaSede_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ObtenerFichaSedeEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosSedes_ResuelveElEndpointDeListarFichasSede_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarFichasSedeEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Par 2 de compatibilidad write-side/read-side (MEF-ADR-0034 seccion 6): este Function App LEE
    // FichaSede mientras el worker la materializa en otro proceso sobre la misma tabla. Sin
    // Schema.For<FichaSede>().UseNumericRevisions, este store espera mt_version uuid sobre la columna
    // bigint del worker, Marten intenta "alter column" en cada request y Postgres responde 42804 (GET
    // en 500 permanente). Espejo de ConfiguracionMartenProjectionsTests
    // .ConfigurarSedes_MaterializaFichaSedeConRevisionNumerica, sin referencia entre ensamblados.
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

    // Tabla, tenancy e IdMember tienen que converger entre el worker y este Function App, o el GET
    // responde 404 para siempre con el daemon funcionando: son dos configuraciones de Marten
    // independientes sobre el mismo schema.
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

    // Mismo par 2 para UbicacionDispositivo, que este Function App consulta en la reaccion
    // ResolverSedeDeMarcacion: sin UseNumericRevisions(true) su primera query dispara el 42804 por
    // request. Espejo de ConfiguracionMartenProjectionsTests (Projections.Tests)
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

    // Sin AgregarWolverinePrivateEventRouter() el FunctionEndpoint compila igual y revienta al primer
    // mensaje del bus.
    [Fact]
    public async Task AgregarServiciosSedes_ResuelveIPrivateEventRouter_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IPrivateEventRouter>();

        act.Should().NotThrow();
    }

    // El lector del read-side propio (los dos lookups de MEF-ADR-0046 paso 2) depende de
    // IDocumentStore + ITenantContext por constructor: si alguno faltara, el hueco solo aparece al
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

    // Con el flip en su default (true) el exporter descarta todo LogRecord emitido dentro de un span
    // no muestreado: con el sampling fraccionario (0.2 por defecto) se pierden en silencio los
    // LogError que alimentan la alerta exception_spike (CA-ADR-0009 Capa 4).
    [Fact]
    public void AgregarServiciosSedes_DeshabilitaElSamplerDeLogsBasadoEnTrazas()
    {
        using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<IOptions<AzureMonitorExporterOptions>>().Value;

        opciones.EnableTraceBasedLogsSampler.Should().BeFalse();
    }

    // El seam rellena ConnectionString con un valor inerte cuando queda vacia (el reader de metricas
    // se construye de forma sincronica y exige una). Ese fallback es seguro SOLO si no pisa una
    // connection string real: si la pisara, trazas y logs se exportarian a un endpoint inexistente,
    // en silencio. Nada garantiza el orden Configure -> PostConfigure ante un upgrade del exporter;
    // este test es esa garantia.
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

    [Fact]
    public async Task AgregarServiciosSedes_DeclaraRevisionNumericaEnTodoDocumentoMarcadoPorProyeccion()
    {
        var marcados = typeof(MaterializadoPorProyeccionAttribute).Assembly.GetTypes()
            .Where(t => t.Namespace == "Bitakora.ControlAsistencia.ReadModels.Sedes"
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
