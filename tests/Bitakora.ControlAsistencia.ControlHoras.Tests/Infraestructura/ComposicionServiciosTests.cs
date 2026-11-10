// Compone el contenedor DI real del dominio (el mismo metodo que invoca Program.cs) y valida que el
// grafo es resoluble sin infra desplegada: ni "dotnet build" ni los unit tests construyen el grafo
// del host, y un hueco ahi solo se veria despues del deploy (HTTP 500 en toda funcion).
//
// Limite conocido: BuildServiceProvider(ValidateOnBuild) no valida registros por factory-lambda
// (Wolverine/Marten registran varios), solo los de tipo mapeado -- de ahi la resolucion explicita
// de ICommandRouter e IPrivateEventRouter en un scope, ademas del build general.
//
// Marten deja deshabilitadas por defecto las columnas de metadata de evento (CorrelationId/
// CausationId/Headers): sin opt-in explicito ni siquiera se crean (MEF-ADR-0034 seccion 7). Sin
// Postgres: el DocumentStore no abre conexion en bootstrap, solo en la primera operacion (Marten 7+).

using System.Globalization;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ReadModels;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using FluentValidation;
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*: vive en JasperFx.MultiTenancy)
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Wolverine;
using ObtenerTurnoVigenteEndpoint = Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoVigente.FunctionEndpoint;
using ObtenerDepuracionDelDiaEndpoint = Bitakora.ControlAsistencia.ControlHoras.ObtenerDepuracionDelDia.FunctionEndpoint;
using ListarTurnosVigentesEndpoint = Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes.FunctionEndpoint;
using ListarAsistenciasDiariasEndpoint = Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias.FunctionEndpoint;
using ListarResumenesAsistenciaEndpoint = Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia.FunctionEndpoint;
using ListarDiasSinProgramarEndpoint = Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar.FunctionEndpoint;
using ListarAdvertenciasProgramacionSemanalEndpoint = Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal.FunctionEndpoint;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class ComposicionServiciosTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    // Copia del literal inline de ComposicionServicios.cs (variable y default del ratio de sampling,
    // CA-ADR-0009 Capa 2): el dominio no declara InternalsVisibleTo hacia este proyecto.
    private const string VariableRatioSampling = "TELEMETRY_SAMPLING_RATIO";
    // El seam nunca lee esta variable (MEF-ADR-0025): se nombra solo para simular su presencia.
    private const string VariableConnectionStringAppInsights = "APPLICATIONINSIGHTS_CONNECTION_STRING";

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

    // Fija la variable mientras corre la accion y restaura el valor original (null la elimina).
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

    // Sampler es una propiedad interna de TracerProviderSdk. Duplicado deliberado de SamplerEfectivo.De
    // en Projections.Tests: no hay ensamblado de test compartido (MEF-ADR-0018, Rule of Three).
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

    // Las banderas de metadata comparten el callback ConfigureMarten con el resolver de serializacion
    // custom, y los round-trip de IntervaloTemporalSerializacionMartenTests no atraviesan el contenedor.
    // El `if (options.Serializer() is SystemTextJsonSerializer)` del wiring omite el resolver EN
    // SILENCIO si el serializador deja de ser STJ, e IntervaloTemporal no sobrevive STJ vanilla.
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

    // Sin registrar los tipos en el EventGraph, toda lectura depende del fallback por mt_dotnet_type en
    // vez del alias (issue #237). Los tipos se listan literalmente: leerlos de
    // IdentidadEventosControlHoras.TiposPersistidos pasaria en verde aunque la lista quedara vacia.
    [Fact]
    public async Task AgregarServiciosControlHoras_RegistraLosTiposDeEventoPersistidos_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertEventosPersistidosRegistrados(
        [
            typeof(MarcacionRegistrada),
            typeof(MarcacionAdicionada),
            typeof(TurnoDiarioAsignado),
            typeof(DepuracionDiaRecibida),
            typeof(SedeDeMarcacionIdentificada),
            typeof(DiaAprobado),
            typeof(DepuracionPosAprobacionRecibida),
            typeof(TurnoDiarioCancelado)
        ]);
    }

    // RequestValidator es fail-open: sin IValidator<T> deja pasar el comando, asi que un validator movido
    // de ensamblado, no publico o un cambio del marker de AddValidatorsFromAssemblyContaining
    // desactivaria en silencio la validacion del endpoint anonimo. Los tests del validator lo instancian
    // directo.
    [Fact]
    public async Task AgregarServiciosControlHoras_DescubreElValidatorDeRegistrarMarcacion_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var validator = scope.ServiceProvider.GetService<IValidator<RegistrarMarcacion>>();

        validator.Should().BeOfType<RegistrarMarcacionValidator>();
    }

    // Registrar el tipo solo sirve si el alias sigue siendo el de las filas ya escritas: aqui se congela
    // sobre el store del contenedor, donde un MapEventType o un EventNamingStyle podrian cambiarlo.
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
            [typeof(TurnoDiarioAsignado)] = "turno_diario_asignado",
            [typeof(DepuracionDiaRecibida)] = "depuracion_dia_recibida",
            [typeof(SedeDeMarcacionIdentificada)] = "sede_de_marcacion_identificada",
            [typeof(DiaAprobado)] = "dia_aprobado",
            [typeof(DepuracionPosAprobacionRecibida)] = "depuracion_pos_aprobacion_recibida",
            [typeof(TurnoDiarioCancelado)] = "turno_diario_cancelado"
        });
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_CongelaElAliasDeCoberturaVecinaActualizada()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var alias = store.Options.Events.AllKnownEventTypes()
            .Single(e => e.EventType == typeof(CoberturaVecinaActualizada)).Alias;

        alias.Should().Be("cobertura_vecina_actualizada");
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_CongelaElAliasDeCierreDeLaVisperaActualizado()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var alias = store.Options.Events.AllKnownEventTypes()
            .Single(e => e.EventType == typeof(CierreDeLaVisperaActualizado)).Alias;

        alias.Should().Be("cierre_de_la_vispera_actualizado");
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_CongelaElAliasDeResolucionDeAmbiguedadDescartada()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var alias = store.Options.Events.AllKnownEventTypes()
            .Single(e => e.EventType == typeof(ResolucionDeAmbiguedadDescartada)).Alias;

        alias.Should().Be("resolucion_de_ambiguedad_descartada");
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_CongelaElAliasDeAmbiguedadDeMarcacionesResuelta()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var alias = store.Options.Events.AllKnownEventTypes()
            .Single(e => e.EventType == typeof(AmbiguedadDeMarcacionesResuelta)).Alias;

        alias.Should().Be("ambiguedad_de_marcaciones_resuelta");
    }

    // Composicion de las Functions (hermano de MEF-ADR-0029): el host isolated las activa por tipo contra
    // el mismo ServiceProvider sin registrarlas, y ActivatorUtilities.CreateInstance reproduce esa
    // activacion sin levantar el host. Solo prueba la RESOLUCION de IDocumentStore/ITenantContext por
    // constructor; el comportamiento de Run lo cubren los unit tests y el smoke test.
    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeObtenerTurnoVigente_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ObtenerTurnoVigenteEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // TurnoVigenteProjection se registra en el worker, donde Marten le aplica UseNumericRevisions
    // (mt_version bigint). Este Function App no puede registrar la proyeccion (CA-ADR-0029), asi que
    // declara la misma forma con Schema.For<TurnoVigente>().UseNumericRevisions(true); sin ella esperaria
    // mt_version uuid sobre la MISMA tabla y ambos lados chocarian en un "alter column" incompatible
    // (42804, 500 permanente). Espejo de ConfiguracionMartenProjectionsTests
    // .ConfigurarControlHoras_MaterializaTurnoVigenteConRevisionNumerica.
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

    // Par 2 de compatibilidad write-side/read-side (MEF-ADR-0034 seccion 6): este Function App lee
    // TurnoVigente sin registrar la proyeccion; tabla, tenancy e IdMember tienen que converger con el
    // worker o el GET devuelve 404 para siempre. El Schema.For<TurnoVigente>() propio puede desviarlos de
    // un solo lado. Espejo de ConfiguracionMartenProjectionsTests
    // .ConfigurarControlHoras_MaterializaTurnoVigenteSobreLaTablaQueConsultaElWriteSide.
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

    // Mismo par que TurnoVigente, para AsistenciaDiaria. Espejo de ConfiguracionMartenProjectionsTests
    // .ConfigurarControlHoras_MaterializaAsistenciaDiariaConRevisionNumerica: los literales cambian en
    // los dos lados a la vez.
    [Fact]
    public async Task AgregarServiciosControlHoras_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaAsistenciaDiaria()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(AsistenciaDiaria));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Par 2 (MEF-ADR-0034 seccion 6) para AsistenciaDiaria: tabla, tenancy e IdMember tienen que
    // converger con el worker o los GET devuelven vacio para siempre; el Schema.For<AsistenciaDiaria>()
    // propio puede desviarlos de un solo lado.
    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveAsistenciaDiariaSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(AsistenciaDiaria));

        mapping.TableName.QualifiedName.Should().Be("control_horas.mt_doc_asistenciadiaria");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(AsistenciaDiaria.Id));
    }

    // Mitad Function App de los dos pares espejo cuya mitad worker congelan, en
    // ConfiguracionMartenProjectionsTests, ..._MaterializaAdvertenciasProgramacionSemanalConRevisionNumerica
    // y ..._MaterializaAdvertenciasProgramacionSemanalSobreLaTablaQueConsultaraElWriteSide: los
    // literales de los dos tests de abajo tienen que cambiar en los dos lados a la vez.
    [Fact]
    public async Task AgregarServiciosControlHoras_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaAdvertenciasProgramacionSemanal()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(AdvertenciasProgramacionSemanal));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveAdvertenciasProgramacionSemanalSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IDocumentStore>()
            .Options.FindOrResolveDocumentType(typeof(AdvertenciasProgramacionSemanal));

        mapping.TableName.QualifiedName.Should().Be("control_horas.mt_doc_advertenciasprogramacionsemanal");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(AdvertenciasProgramacionSemanal.Id));
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeObtenerDepuracionDelDia_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ObtenerDepuracionDelDiaEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeListarTurnosVigentes_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarTurnosVigentesEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeListarAsistenciasDiarias_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarAsistenciasDiariasEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeListarResumenesAsistencia_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarResumenesAsistenciaEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // UseAzureMonitorExporter llama SetSampler internamente con RateLimitedSampler
    // (AzureMonitorExporterOptions.TracesPerSecond = 5.0 por defecto, Exporter 1.8.1) y pisa un
    // SetSampler previo: el ParentBasedSampler de CA-ADR-0009 Capa 2 solo gana si se configura DESPUES
    // (un segundo .WithTracing(...)). Por eso se verifica el sampler EFECTIVO del contenedor.
    // ControlHoras no corre daemon de Marten: sin el wrapper SamplerQueDescartaPollingDelDaemon de
    // Projections.
    [Fact]
    public void AgregarServiciosControlHoras_ResuelveElSamplerConfiguradoPorElProyecto_EnVezDeRateLimitedSampler()
    {
        // TracerProvider es Singleton IDisposable: basta el Dispose() sincrono, sin scope.
        using var provider = ComponerServiceProvider();
        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        var samplerEfectivo = ObtenerSamplerEfectivo(tracerProvider);

        // RateLimitedSampler es internal en otro ensamblado: se compara por nombre completo.
        samplerEfectivo.GetType().FullName.Should().NotBe(
            "Azure.Monitor.OpenTelemetry.Exporter.Internals.RateLimitedSampler");
        samplerEfectivo.Should().BeOfType<ParentBasedSampler>();
    }

    // Verifica que el ratio llega al sampler compuesto, no solo que se calcula. Sampler.Description es
    // publica: "ParentBased{TraceIdRatioBasedSampler{F6}}".
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

    // TELEMETRY_SAMPLING_RATIO no esta puesta en ningun recurso desplegado (medido en el issue #308): el
    // camino que corre en dev es el del DEFAULT, y un ratio distinto solo se veria como volumen de
    // ingestion inesperado.
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

    // PersistenceMetrics.StartPolling es un PeriodicTimer (default 5s) que llama
    // store.Admin.FetchCountsAsync(): ~123.000 spans/24h en dev de metricas que nadie consume, y
    // CheckHealthAsync llama FetchCountsAsync por su cuenta.
    //
    // Se resuelve WolverineOptions del CONTENEDOR REAL porque el riesgo es de ORDEN:
    // AgregarWolverineParaComandosServerless (Cosmos.EventSourcing.CritterStack 2.3.1) corre el callback
    // del consumidor PRIMERO y asigna Durability.Mode = Solo DESPUES; hoy no pisa
    // DurabilityMetricsEnabled, pero un upgrade podria.
    //
    // await using: WolverineOptions es Singleton por factory-lambda y solo implementa IAsyncDisposable;
    // el Dispose() sincrono del provider raiz lanzaria InvalidOperationException.
    [Fact]
    public async Task AgregarServiciosControlHoras_ApagaLaRecoleccionDeMetricasDeDurabilidad_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityMetricsEnabled.Should().BeFalse();
    }

    // Se apaga la recoleccion de METRICAS, no la durabilidad real (recovery, scheduled jobs, dead
    // letters): fijar Mode evita que la correccion se resuelva apagando la durabilidad completa.
    [Fact]
    public async Task AgregarServiciosControlHoras_ConservaLaDurabilidadReal_CuandoApagaLasMetricasDeCola()
    {
        await using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<WolverineOptions>();

        opciones.Durability.DurabilityAgentEnabled.Should().BeTrue();
        opciones.Durability.Mode.Should().Be(DurabilityMode.Solo);
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeReady_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ReadyCheck>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    // Con EnableTraceBasedLogsSampler en su default (true) el exporter descarta todo LogRecord emitido
    // dentro de un span no muestreado: con ratio 0.2 se perderian en silencio los LogError que alimentan
    // la alerta exception_spike (CA-ADR-0009 Capa 4).
    [Fact]
    public void AgregarServiciosControlHoras_DeshabilitaElSamplerDeLogsBasadoEnTrazas()
    {
        using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<IOptions<AzureMonitorExporterOptions>>().Value;

        opciones.EnableTraceBasedLogsSampler.Should().BeFalse();
    }

    // El PostConfigure del seam rellena ConnectionString con un valor inerte cuando esta vacia (el reader
    // de metricas exige una al construirse y la Key Vault reference puede no estar resuelta en frio). Si
    // pisara una connection string real, trazas y logs se exportarian en silencio a un endpoint
    // inexistente; nada en el codigo garantiza el orden Configure -> PostConfigure frente a un upgrade
    // del exporter, este test si.
    [Fact]
    public void AgregarServiciosControlHoras_ConservaLaConnectionStringReal_CuandoLaVariableDeEntornoEstaPresente()
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
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeListarAdvertenciasProgramacionSemanal_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarAdvertenciasProgramacionSemanalEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveElEndpointDeListarDiasSinProgramar_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => ActivatorUtilities.CreateInstance<ListarDiasSinProgramarEndpoint>(scope.ServiceProvider);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_DeclaraRevisionNumericaEnTodoDocumentoMarcadoPorProyeccion()
    {
        var marcados = typeof(MaterializadoPorProyeccionAttribute).Assembly.GetTypes()
            .Where(t => t.Namespace == "Bitakora.ControlAsistencia.ReadModels.ControlHoras"
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
