using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Projections.Tests;

// Config-test del worker de proyecciones (MEF-ADR-0034 seccion 6, hermano de MEF-ADR-0029).
// Invoca cada Configurar{Dominio} directamente -- nunca a traves de ConfiguracionMartenProjections
// .ConfigurarEventos, que es wiring puro para Program.cs y queda fuera de esta medicion -- con una
// cadena de conexion dummy, sin necesidad de Postgres real (Marten 7+ no abre la conexion durante
// el bootstrapping del IHost). Cada dominio se cubre con las mismas guardas: el named store
// resuelve del contenedor, apunta al schema del write-side, replica su metadata de evento y su
// identidad de stream, no tiene ninguna proyeccion Inline y corre su daemon en HotCold. Issue #268
// (CA-1/CA-2, MEF-ADR-0034 seccion 6 enmendada por #447 del marco): ademas replica la tenancy y el
// naming de eventos del write-side, la politica de documentos multi-tenant (par 2 del issue) y el
// resolver de serializacion custom (fuente unica con el write-side, MEF-ADR-0029). La superficie de
// Marten que cada una interroga vive en AssertsProyecciones.
public class ConfiguracionMartenProjectionsTests
{
    private const string ConnectionStringDummy = "Host=localhost;Database=dummy";

    // Issue #268 CA-2: identificadores fijos para los canarios de round-trip del resolver -- el
    // valor en si es irrelevante, solo debe ser estable para que el test sea reproducible.
    private static readonly Guid TurnoIdCanario = Guid.Parse("019600a0-0000-7000-8000-000000000099");

    private static ServiceProvider CrearProvider(Action<IServiceCollection> configurarDominio)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        configurarDominio(services);

        return services.BuildServiceProvider();
    }

    private static ServiceProvider ProviderDeProgramacion() =>
        CrearProvider(services => services.ConfigurarProgramacion(ConnectionStringDummy));

    private static ServiceProvider ProviderDeControlHoras() =>
        CrearProvider(services => services.ConfigurarControlHoras(ConnectionStringDummy));

    // --- Programacion (CA-1, CA-3, CA-6, CA-7) ---

    [Fact]
    public void ConfigurarProgramacion_ResuelveElNamedStoreDelDominio()
    {
        using var provider = ProviderDeProgramacion();

        var store = provider.GetService<IProgramacionProjectionStore>();

        store.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurarProgramacion_RegistraElNamedStoreSobreElSchemaDeProgramacion()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertSchema("programacion");
    }

    [Fact]
    public void ConfigurarProgramacion_ReplicaLaMetadataDeEventoDelWriteSide()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertOpcionesDeEvento();
    }

    [Fact]
    public void ConfigurarProgramacion_NoRegistraNingunaProyeccionInline()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertSinProyeccionesInline();
    }

    [Fact]
    public void ConfigurarProgramacion_EnciendeElDaemonEnModoHotCold()
    {
        using var provider = ProviderDeProgramacion();

        provider.AssertDaemonHotCold<IProgramacionProjectionStore>();
    }

    [Fact]
    public void ConfigurarProgramacion_DeclaraLaStreamIdentityComoString()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertStreamIdentityAsString();
    }

    // Issue #268 CA-1: tenancy de eventos conjoined, mismo modelo con el que el write-side escribio
    // el event store.
    [Fact]
    public void ConfigurarProgramacion_DeclaraTenancyDeEventosConjoined()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertTenancyDeEventosConjoined();
    }

    // Issue #268 CA-1: naming de eventos SmarterTypeName, replica del write-side.
    [Fact]
    public void ConfigurarProgramacion_DeclaraEventNamingStyleSmarterTypeName()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertEventNamingStyleSmarterTypeName();
    }

    // Issue #268 CA-1: Policies.AllDocumentsAreMultiTenanted() -- gobierna el par 2 (worker ->
    // query-side), la forma de la tabla de cualquier read model futuro de este dominio.
    [Fact]
    public void ConfigurarProgramacion_DeclaraLosDocumentosComoMultiTenant()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertDocumentosMultiTenant<DocumentoCanarioTenancy>();
    }

    // Issue #268 CA-2: el resolver custom de Programacion (misma fuente que invoca el write-side,
    // MEF-ADR-0029) sobrevive el round-trip contra el ISerializer real del named store.
    // TurnoCreado.Crear tiene ambos constructores privados -- STJ no puede reconstruirlo sin el
    // resolver, ni si UseSystemTextJsonForSerialization se invoca despues de engancharlo.
    [Fact]
    public void ConfigurarProgramacion_ConservaElResolverDeSerializacionCustom()
    {
        using var provider = ProviderDeProgramacion();
        var evento = TurnoCreado.Crear(
            TurnoIdCanario,
            "Turno Canario",
            [new DatosFranja(
                new TimeOnly(6, 0), new TimeOnly(14, 0),
                [(new TimeOnly(10, 0), new TimeOnly(10, 15))],
                [(new TimeOnly(6, 0), new TimeOnly(6, 30))])]);

        var restaurado = provider.GetRequiredService<IProgramacionProjectionStore>()
            .DeserializarConResolverDeSerializacionCustom(evento);

        restaurado.Should().NotBeNull();
        restaurado.TurnoId.Should().Be(TurnoIdCanario);
        restaurado.Nombre.Should().Be("Turno Canario");
        restaurado.FranjasOrdinarias.Should().HaveCount(1);
        restaurado.FranjasOrdinarias[0].ToString()
            .Should().Be("(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(06:00-06:30)]");
    }

    // Issue #277 CA-3/CA-4: defensa en profundidad read-side. El worker no esta expuesto hoy (sin
    // proyecciones concretas), pero lo estara en cuanto las tenga -- este guardrail evita que ese
    // dia el daemon lea streams preexistentes sin el tipo registrado en su propio EventGraph.
    //
    // Tipos esperados listados literalmente (oraculo independiente, MEF-ADR-0002): leerlos de
    // IdentidadEventosProgramacion.TiposPersistidos acoplaria este guardrail al mismo artefacto que
    // IdentidadEventosProgramacionTests ya verifica en el write-side.
    [Fact]
    public void ConfigurarProgramacion_RegistraLosTiposDeEventoPersistidos()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertEventosPersistidosRegistrados([typeof(TurnoCreado), typeof(ProgramacionTurnoSolicitada)]);
    }

    // --- ControlHoras (CA-2, CA-3, CA-6, CA-7) ---

    [Fact]
    public void ConfigurarControlHoras_ResuelveElNamedStoreDelDominio()
    {
        using var provider = ProviderDeControlHoras();

        var store = provider.GetService<IControlHorasProjectionStore>();

        store.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurarControlHoras_RegistraElNamedStoreSobreElSchemaDeControlHoras()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertSchema("control_horas");
    }

    [Fact]
    public void ConfigurarControlHoras_ReplicaLaMetadataDeEventoDelWriteSide()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertOpcionesDeEvento();
    }

    [Fact]
    public void ConfigurarControlHoras_NoRegistraNingunaProyeccionInline()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertSinProyeccionesInline();
    }

    [Fact]
    public void ConfigurarControlHoras_EnciendeElDaemonEnModoHotCold()
    {
        using var provider = ProviderDeControlHoras();

        provider.AssertDaemonHotCold<IControlHorasProjectionStore>();
    }

    [Fact]
    public void ConfigurarControlHoras_DeclaraLaStreamIdentityComoString()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertStreamIdentityAsString();
    }

    // Issue #268 CA-1: tenancy de eventos conjoined, mismo modelo con el que el write-side escribio
    // el event store.
    [Fact]
    public void ConfigurarControlHoras_DeclaraTenancyDeEventosConjoined()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertTenancyDeEventosConjoined();
    }

    // Issue #268 CA-1: naming de eventos SmarterTypeName, replica del write-side.
    [Fact]
    public void ConfigurarControlHoras_DeclaraEventNamingStyleSmarterTypeName()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertEventNamingStyleSmarterTypeName();
    }

    // Issue #268 CA-1: Policies.AllDocumentsAreMultiTenanted() -- gobierna el par 2 (worker ->
    // query-side), la forma de la tabla de cualquier read model futuro de este dominio.
    [Fact]
    public void ConfigurarControlHoras_DeclaraLosDocumentosComoMultiTenant()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertDocumentosMultiTenant<DocumentoCanarioTenancy>();
    }

    // Issue #268 CA-2: el resolver custom de ControlHoras (misma fuente que invoca el write-side,
    // MEF-ADR-0029) sobrevive el round-trip contra el ISerializer real del named store.
    // MarcacionRegistrada.Crear tiene ambos constructores privados -- STJ no puede reconstruirlo sin
    // el resolver, ni si UseSystemTextJsonForSerialization se invoca despues de engancharlo.
    [Fact]
    public void ConfigurarControlHoras_ConservaElResolverDeSerializacionCustom()
    {
        using var provider = ProviderDeControlHoras();
        var evento = MarcacionRegistrada.Crear(
            "1098765432", new DateTime(2026, 7, 31, 8, 0, 0), "Entrada", "disp-01");

        var restaurado = provider.GetRequiredService<IControlHorasProjectionStore>()
            .DeserializarConResolverDeSerializacionCustom(evento);

        restaurado.Should().NotBeNull();
        restaurado.EmpleadoId.Should().Be("1098765432");
        restaurado.TimestampNormalizado.Should().Be(new DateTime(2026, 7, 31, 8, 0, 0));
        restaurado.TipoMarcacion.Should().Be("Entrada");
        restaurado.DispositivoId.Should().Be("disp-01");
    }

    // Issue #277 CA-3/CA-4: defensa en profundidad read-side. El worker no esta expuesto hoy (sin
    // proyecciones concretas), pero lo estara en cuanto las tenga -- este guardrail evita que ese
    // dia el daemon lea streams preexistentes sin el tipo registrado en su propio EventGraph.
    //
    // Tipos esperados listados literalmente (oraculo independiente, MEF-ADR-0002): leerlos de
    // IdentidadEventosControlHoras.TiposPersistidos acoplaria este guardrail al mismo artefacto que
    // IdentidadEventosControlHorasTests ya verifica en el write-side.
    [Fact]
    public void ConfigurarControlHoras_RegistraLosTiposDeEventoPersistidos()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertEventosPersistidosRegistrados(
                [typeof(MarcacionRegistrada), typeof(MarcacionAdicionada), typeof(TurnoDiarioAsignado)]);
    }

    // --- Seam de nivel BC (CA-4) ---

    // Las guardas de arriba invocan cada Configurar{Dominio} directamente, asi que quedan verdes
    // aunque nadie encadene esa llamada en ConfigurarEventos -- y Program.cs solo invoca
    // ConfigurarEventos. Sin esta guarda, un dominio con su seam implementado pero sin encadenar
    // compila limpio, pasa el resto del config-test y su daemon nunca corre en produccion.
    [Fact]
    public void ConfigurarEventos_RegistraElNamedStoreDeCadaDominioDelBc()
    {
        using var provider = CrearProvider(services => services.ConfigurarEventos(ConnectionStringDummy));

        provider.GetService<IProgramacionProjectionStore>().Should().NotBeNull();
        provider.GetService<IControlHorasProjectionStore>().Should().NotBeNull();
    }
}
