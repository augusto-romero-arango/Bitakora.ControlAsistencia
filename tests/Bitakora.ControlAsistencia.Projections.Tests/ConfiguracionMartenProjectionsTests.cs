using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*: vive en JasperFx.MultiTenancy)
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

    // Issue #268 CA-2: identidad del canario de round-trip de Programacion (el de ControlHoras,
    // MarcacionRegistrada, se identifica por EmpleadoId string y no necesita ninguna). El valor en
    // si es irrelevante: solo debe ser estable para que el test sea reproducible.
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

    // Issue #289 CA-4: primera proyeccion concreta del BC. TurnoDiarioProjection (N1,
    // SingleStreamProjection<TurnoDiarioView, string>) queda registrada con lifecycle Async, el
    // canonico del worker (MEF-ADR-0034 seccion 3). Complementa
    // ConfigurarControlHoras_NoRegistraNingunaProyeccionInline: aquella prueba que NADA quedo
    // Inline -- una lista vacia la pasaria --, esta prueba que la proyeccion CONCRETA si se registro.
    [Fact]
    public void ConfigurarControlHoras_RegistraTurnoDiarioProjectionComoAsync()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertProyeccionAsyncRegistrada("TurnoDiarioView");
    }

    // Issue #289 CA-5, mitad worker del punto abierto de MEF-ADR-0035 seccion 4: el daemon escribe
    // TurnoDiarioView desde este named store y el Function App de ControlHoras la lee, en otro
    // proceso, con session.LoadAsync y sin registrar la proyeccion. Que ambos lados converjan en la
    // MISMA tabla fisica y la MISMA tenancy no lo garantiza ningun compilador.
    //
    // El oraculo es el literal de la tabla, y ComposicionServiciosTests
    // .AgregarServiciosControlHoras_ResuelveTurnoDiarioViewSobreLaTablaQueMaterializaElWorker...
    // congela el MISMO literal desde el write-side: juntos pinean las dos mitades sin que ningun
    // ensamblado tenga que referenciar al otro (CA-ADR-0028/CA-ADR-0029). Si divergieran, el GET
    // devolveria 404 para siempre con el daemon funcionando -- fallo silencioso que solo detectaria
    // el smoke test contra dev (CA-8), fuera del CI de PR.
    [Fact]
    public void ConfigurarControlHoras_MaterializaTurnoDiarioViewSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeControlHoras();

        var mapping = provider.GetRequiredService<IControlHorasProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(TurnoDiarioView));

        mapping.TableName.QualifiedName.Should().Be("control_horas.mt_doc_turnodiarioview");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(TurnoDiarioView.Id));
    }

    // Issue #328 CA-3: segunda proyeccion concreta del dominio (N1, SingleStreamProjection
    // <TurnoVigente, string>, mismo stream que TurnoDiarioProjection -- (EmpleadoId, Fecha)).
    // Complementa ConfigurarControlHoras_NoRegistraNingunaProyeccionInline (que solo prueba que
    // NADA quedo Inline; una lista sin TurnoVigente la pasaria igual) verificando que la proyeccion
    // CONCRETA de este issue si quedo registrada con lifecycle Async. FASE ROJA: hoy el seam
    // (ConfiguracionMartenProjectionsControlHoras.ConfigurarControlHoras) todavia no encadena
    // opts.Projections.Add<TurnoVigenteProjection>(...) -- eso es alcance de projection-implementer.
    [Fact]
    public void ConfigurarControlHoras_RegistraTurnoVigenteProjectionComoAsync()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertProyeccionAsyncRegistrada("TurnoVigente");
    }

    // Issue #328, mismo gotcha de "Numeric Revisioned Documents" que ya peno a TurnoDiarioView
    // (issue #294): Marten aplica ProjectionDocumentPolicy SOLO a los documentos que son target de
    // una proyeccion REGISTRADA en el store (UseNumericRevisions = true, Metadata.Revision --
    // mt_version bigint -- habilitada, Metadata.Version -- mt_version uuid -- deshabilitada). Hasta
    // que TurnoVigenteProjection se registre arriba, este mapping cae al default (Version
    // habilitado, Revision deshabilitado) y este test queda en rojo.
    //
    // El oraculo es literal y espejo del que ComposicionServiciosTests
    // .AgregarServiciosControlHoras_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_
    // ParaTurnoVigente congela desde el write-side -- juntos cierran la misma dimension que #294
    // tuvo que cerrar para TurnoDiarioView, antes de que un 42804 llegue a dev.
    [Fact]
    public void ConfigurarControlHoras_MaterializaTurnoVigenteConRevisionNumerica()
    {
        using var provider = ProviderDeControlHoras();

        var mapping = provider.GetRequiredService<IControlHorasProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(TurnoVigente));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Issue #294, mitad worker de la dimension que el par de #289 dejo abierta: la guarda de arriba
    // pinea tabla, tenancy e Id, y las tres convergian -- el 500 en dev entro por la forma de
    // mt_version, que nadie media.
    //
    // Este lado NO declara nada para que estos valores sean asi: los impone Marten al registrar
    // TurnoDiarioProjection, via ProjectionDocumentPolicy, sobre todo documento target de una
    // proyeccion registrada en el store (https://martendb.io/documents/concurrency, "Numeric
    // Revisioned Documents"; Marten/Events/Projections/ProjectionDocumentPolicy.cs). O sea que este
    // es el lado que DEFINE la forma fisica de la tabla, y el write-side es el que debe replicarla.
    // Por eso el oraculo se congela aqui tambien: si una version futura de Marten cambiara el tipo
    // por defecto de Metadata.Revision, este test se pondria rojo junto con su hermano
    // ComposicionServiciosTests.AgregarServiciosControlHoras_EsperaLaMismaColumnaDeVersionQueMaterializaElWorker...
    // en vez de dejar que la divergencia llegue a dev como un 42804 por request.
    [Fact]
    public void ConfigurarControlHoras_MaterializaTurnoDiarioViewConRevisionNumerica()
    {
        using var provider = ProviderDeControlHoras();

        var mapping = provider.GetRequiredService<IControlHorasProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(TurnoDiarioView));

        // Mismos literales que congela el write-side. Las dos columnas comparten el nombre fisico
        // mt_version y solo una puede estar habilitada, de ahi que se midan ambas.
        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
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
