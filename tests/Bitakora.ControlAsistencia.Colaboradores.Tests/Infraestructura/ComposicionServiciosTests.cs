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
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Wolverine;

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
    // Rojo esperado (fase roja, issue #330): TiposPersistidos sigue vacio.
    [Fact]
    public async Task AgregarServiciosColaboradores_RegistraLosTiposDeEventoPersistidos_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertEventosPersistidosRegistrados(
            [typeof(ColaboradorRegistrado), typeof(VinculacionIniciada)]);
    }

    // Issue #330: registrar el tipo solo sirve si el alias sigue siendo el que las filas ya
    // escritas llevan en su columna "type". AliasEventosColaboradoresTests lo congela sobre un
    // StoreOptions standalone; esta guarda lo congela sobre el store del contenedor, el unico lugar
    // donde un MapEventType o un EventNamingStyle agregados al wiring podrian cambiarlo.
    // Rojo esperado (fase roja, issue #330): TiposPersistidos sigue vacio, AllKnownEventTypes() no
    // trae ninguna de las dos claves del diccionario esperado.
    [Fact]
    public async Task AgregarServiciosColaboradores_DerivaElAliasDeEventoDelNombreDeClase_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertAliasDeEventosPersistidos(new Dictionary<Type, string>
        {
            [typeof(ColaboradorRegistrado)] = "colaborador_registrado",
            [typeof(VinculacionIniciada)] = "vinculacion_iniciada"
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
}
