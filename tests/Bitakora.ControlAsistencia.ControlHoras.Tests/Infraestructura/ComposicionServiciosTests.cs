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

using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using FluentValidation;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class ComposicionServiciosTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

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
}
