using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*: vive en JasperFx.MultiTenancy)
using Microsoft.Extensions.DependencyInjection;
using Weasel.Postgresql.Tables; // IndexMethod/IndexDefinition (NO Marten.*: viven en Weasel.Postgresql.Tables)

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
    // MarcacionRegistrada, se identifica por CodigoColaborador string y no necesita ninguna). El valor en
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

    private static ServiceProvider ProviderDeColaboradores() =>
        CrearProvider(services => services.ConfigurarColaboradores(ConnectionStringDummy));

    private static ServiceProvider ProviderDeSedes() =>
        CrearProvider(services => services.ConfigurarSedes(ConnectionStringDummy));

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

    // Issue #496 CA-5: primera proyeccion concreta del dominio Programacion (N1,
    // SingleStreamProjection<FichaTurno, string> sobre el stream del catalogo de turnos,
    // CatalogoTurnos). Complementa ConfigurarProgramacion_NoRegistraNingunaProyeccionInline:
    // aquella prueba que NADA quedo Inline -- una lista vacia la pasaria --, esta prueba que la
    // proyeccion CONCRETA se registro con lifecycle Async, el canonico del worker (MEF-ADR-0034
    // seccion 3). El seam (ConfiguracionMartenProjectionsProgramacion.ConfigurarProgramacion) existe
    // desde el issue #268 sin ninguna proyeccion; este issue le agrega la unica linea
    // opts.Projections.Add<FichaTurnoProjection>(ProjectionLifecycle.Async).
    [Fact]
    public void ConfigurarProgramacion_RegistraFichaTurnoProjectionComoAsync()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertProyeccionAsyncRegistrada("FichaTurno");
    }

    // Issue #496, mismo gotcha de "Numeric Revisioned Documents" que #328/#356/#461 ya cerraron
    // para TurnoVigente/FichaColaborador/FichaSede: Marten aplica ProjectionDocumentPolicy SOLO a
    // los documentos target de una proyeccion REGISTRADA en el store (UseNumericRevisions = true,
    // Metadata.Revision -- mt_version bigint -- habilitada, Metadata.Version -- mt_version uuid --
    // deshabilitada). Si FichaTurnoProjection dejara de registrarse arriba, este mapping caeria al
    // default y este test se pondria rojo.
    //
    // Este lado NO declara nada para que los valores sean asi: los impone Marten al registrar la
    // proyeccion. Es el lado que DEFINE la forma fisica de la tabla y el write-side el que debe
    // replicarla -- por eso el oraculo se congela aqui tambien.
    //
    // Espejo de ComposicionServiciosTests (Programacion.Tests)
    // .AgregarServiciosProgramacion_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaFichaTurno.
    [Fact]
    public void ConfigurarProgramacion_MaterializaFichaTurnoConRevisionNumerica()
    {
        using var provider = ProviderDeProgramacion();

        var mapping = provider.GetRequiredService<IProgramacionProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaTurno));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Issue #496, mitad worker del par 2 de compatibilidad write-side/read-side (MEF-ADR-0034
    // seccion 6): el daemon materializa FichaTurno desde este named store y el Function App de
    // Programacion la lee, en otro proceso, con session.LoadAsync/Query (ObtenerFichaTurno,
    // ListarFichasTurno). Que ambos lados converjan en la MISMA tabla fisica, la MISMA tenancy y el
    // MISMO IdMember no lo garantiza ningun compilador -- una divergencia deja el GET en 404
    // permanente con el daemon funcionando.
    //
    // Espejo de ComposicionServiciosTests (Programacion.Tests)
    // .AgregarServiciosProgramacion_ResuelveFichaTurnoSobreLaTablaQueMaterializaElWorker_....
    [Fact]
    public void ConfigurarProgramacion_MaterializaFichaTurnoSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeProgramacion();

        var mapping = provider.GetRequiredService<IProgramacionProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaTurno));

        mapping.TableName.QualifiedName.Should().Be("programacion.mt_doc_fichaturno");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(FichaTurno.Id));
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
        restaurado.CodigoColaborador.Should().Be("1098765432");
        restaurado.TimestampNormalizado.Should().Be(new DateTime(2026, 7, 31, 8, 0, 0));
        restaurado.TipoMarcacion.Should().Be("Entrada");
        restaurado.DispositivoId.Should().Be("disp-01");
    }

    // Issue #277 CA-3/CA-4: defensa en profundidad read-side -- evita que el daemon lea streams
    // preexistentes sin el tipo registrado en su propio EventGraph.
    //
    // Tipos esperados listados literalmente (oraculo independiente, MEF-ADR-0002): leerlos de
    // IdentidadEventosControlHoras.TiposPersistidos acoplaria este guardrail al mismo artefacto que
    // IdentidadEventosControlHorasTests ya verifica en el write-side. Debe seguir siendo espejo del
    // oraculo del write-side (ComposicionServiciosTests
    // .AgregarServiciosControlHoras_RegistraLosTiposDeEventoPersistidos_...): un tipo listado alli y
    // no aqui es exactamente la divergencia que este guardrail existe para cazar.
    [Fact]
    public void ConfigurarControlHoras_RegistraLosTiposDeEventoPersistidos()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertEventosPersistidosRegistrados(
            [
                typeof(MarcacionRegistrada),
                typeof(MarcacionAdicionada),
                typeof(TurnoDiarioAsignado),
                typeof(DepuracionDiaRecibida)
            ]);
    }

    // Issue #328 CA-3: proyeccion concreta del dominio (N1, SingleStreamProjection<TurnoVigente,
    // string> sobre el stream (CodigoColaborador, Fecha)). Complementa
    // ConfigurarControlHoras_NoRegistraNingunaProyeccionInline: aquella prueba que NADA quedo
    // Inline -- una lista vacia la pasaria --, esta prueba que la proyeccion CONCRETA si se
    // registro con lifecycle Async, el canonico del worker (MEF-ADR-0034 seccion 3).
    [Fact]
    public void ConfigurarControlHoras_RegistraTurnoVigenteProjectionComoAsync()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertProyeccionAsyncRegistrada("TurnoVigente");
    }

    // Issue #328, mismo gotcha de "Numeric Revisioned Documents" que el issue #294 ya peno sobre el
    // read model anterior (retirado por #323): Marten aplica ProjectionDocumentPolicy SOLO a los
    // documentos que son target de una proyeccion REGISTRADA en el store (UseNumericRevisions =
    // true, Metadata.Revision -- mt_version bigint -- habilitada, Metadata.Version -- mt_version
    // uuid -- deshabilitada). Si TurnoVigenteProjection dejara de registrarse arriba, este mapping
    // caeria al default (Version habilitado, Revision deshabilitado) y este test se pondria rojo.
    //
    // Este lado NO declara nada para que los valores sean asi: los impone Marten al registrar la
    // proyeccion (https://martendb.io/documents/concurrency, "Numeric Revisioned Documents";
    // Marten/Events/Projections/ProjectionDocumentPolicy.cs). O sea que este es el lado que DEFINE
    // la forma fisica de la tabla y el write-side el que debe replicarla -- por eso el oraculo se
    // congela aqui tambien: si una version futura de Marten cambiara el tipo por defecto de
    // Metadata.Revision, ambos lados se pondrian rojos juntos en vez de dejar que la divergencia
    // llegue a dev como un 42804 por request.
    //
    // El oraculo es literal y espejo del que ComposicionServiciosTests
    // .AgregarServiciosControlHoras_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_
    // ParaTurnoVigente congela desde el write-side -- juntos cierran la misma dimension que #294
    // tuvo que cerrar en su momento, antes de que un 42804 llegue a dev.
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

    // Issue #328, mitad worker del par 2 de compatibilidad write-side/read-side (MEF-ADR-0034
    // seccion 6): el daemon materializa TurnoVigente desde este named store y el Function App de
    // ControlHoras la lee, en otro proceso, con session.LoadAsync. Misma guarda que #289 dejo para
    // el read model anterior: que ambos lados converjan en la MISMA tabla fisica, la MISMA tenancy
    // y el MISMO IdMember no lo garantiza ningun compilador -- son dos configuraciones de Marten
    // independientes sobre el mismo schema, y una divergencia deja el GET en 404 permanente con el
    // daemon funcionando (fallo silencioso que solo veria el smoke test contra dev).
    //
    // Anadida en la revision de #328: la fase roja la omitio por no tener potencial de rojo (Marten
    // resuelve los tres valores por convencion), pero el write-side SI declara ahora un
    // Schema.For<TurnoVigente>() propio -- un punto por donde una divergencia futura puede entrar sin
    // que nadie la mida. El oraculo es literal y ComposicionServiciosTests
    // .AgregarServiciosControlHoras_ResuelveTurnoVigenteSobreLaTablaQueMaterializaElWorker... congela
    // el MISMO literal desde el write-side, sin que ningun ensamblado referencie al otro
    // (CA-ADR-0028/CA-ADR-0029).
    [Fact]
    public void ConfigurarControlHoras_MaterializaTurnoVigenteSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeControlHoras();

        var mapping = provider.GetRequiredService<IControlHorasProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(TurnoVigente));

        mapping.TableName.QualifiedName.Should().Be("control_horas.mt_doc_turnovigente");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(TurnoVigente.Id));
    }

    // Complementa ConfigurarControlHoras_NoRegistraNingunaProyeccionInline: aquella prueba que NADA
    // quedo Inline -- una lista vacia la pasaria --, esta que la proyeccion CONCRETA se registro con
    // lifecycle Async, el canonico del worker (MEF-ADR-0034 seccion 3).
    [Fact]
    public void ConfigurarControlHoras_RegistraAsistenciaDiariaProjectionComoAsync()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertProyeccionAsyncRegistrada("AsistenciaDiaria");
    }

    // Mismo gotcha de "Numeric Revisioned Documents" que documenta
    // ConfigurarControlHoras_MaterializaTurnoVigenteConRevisionNumerica: ProjectionDocumentPolicy
    // aplica POR DOCUMENTO target de una proyeccion registrada, asi que cada vista nueva necesita su
    // propia guarda -- la de TurnoVigente no la cubre. Este lado no declara nada: los valores los
    // impone Marten al registrar AsistenciaDiariaProjection arriba.
    [Fact]
    public void ConfigurarControlHoras_MaterializaAsistenciaDiariaConRevisionNumerica()
    {
        using var provider = ProviderDeControlHoras();

        var mapping = provider.GetRequiredService<IControlHorasProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(AsistenciaDiaria));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // --- Colaboradores (issue #330: el dominio estrena sus dos primeros eventos persistidos) ---

    // Issue #330 (par 1 de MEF-ADR-0034 seccion 6, fila "Tipos de evento registrados"): el read-side
    // reconoce los tipos que el write-side acaba de empezar a persistir. Hasta este issue
    // IdentidadEventosColaboradores.TiposPersistidos estaba vacio y no habia nada que alinear.
    //
    // Tipos esperados listados literalmente (oraculo independiente, MEF-ADR-0002): leerlos de
    // TiposPersistidos acoplaria el guardrail al mismo artefacto que ya verifica el write-side.
    [Fact]
    public void ConfigurarColaboradores_RegistraLosTiposDeEventoPersistidos()
    {
        using var provider = ProviderDeColaboradores();

        provider.GetRequiredService<IColaboradoresProjectionStore>()
            .AssertEventosPersistidosRegistrados(
                [typeof(ColaboradorRegistrado), typeof(VinculacionIniciada)]);
    }

    // Issue #330 (par 1 de MEF-ADR-0034 seccion 6, fila "Serializador"): ColaboradorRegistrado lleva
    // payload rico -- Identificacion y NombreColaborador son sealed class con campos privados y
    // ConfigurarSerializacion (#348) --, asi que el write-side instala un TypeInfoResolver custom
    // dentro de su ConfigureMarten. Sin la MISMA fuente instalada en este named store, el dia que
    // este dominio registre su primera proyeccion el daemon leeria colaborador_registrado con STJ
    // vanilla y reventaria con NotSupportedException en runtime, no en el build (el mismo canal que
    // ColaboradorRegistradoSerializacionTests.Deserializar_LanzaNotSupportedException_... documenta).
    // Fuente unica con el write-side (MEF-ADR-0029): se invoca la misma clase, nunca una copia.
    [Fact]
    public void ConfigurarColaboradores_ConservaElResolverDeSerializacionCustom()
    {
        using var provider = ProviderDeColaboradores();
        var evento = new ColaboradorRegistrado(
            Identificacion.Crear(TipoIdentificacion.CC, "79543210"),
            NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Prieto"));

        var restaurado = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .DeserializarConResolverDeSerializacionCustom(evento);

        restaurado.Should().NotBeNull();
        // Issue #381: el separador de la llave de Identificacion cambia de ":" a "-".
        restaurado.Identificacion.ToString().Should().Be("CC-79543210");
        restaurado.Nombre.NombreCompleto.Should().Be("Luis Augusto Barreto Prieto");
    }

    // Issue #356 CA-1..CA-5: primera proyeccion concreta del dominio Colaboradores (N1,
    // SingleStreamProjection<FichaColaborador, string> sobre el stream de ColaboradorAggregateRoot).
    // Complementa ConfigurarColaboradores_RegistraLosTiposDeEventoPersistidos: aquella prueba que
    // los TIPOS de evento estan registrados, esta prueba que la PROYECCION concreta que los
    // consume esta registrada en el named store con lifecycle Async, el canonico del worker
    // (MEF-ADR-0034 seccion 3). El seam (ConfiguracionMartenProjectionsColaboradores.
    // ConfigurarColaboradores) existe desde el issue #360; este issue le agrega la unica linea
    // opts.Projections.Add<FichaColaboradorProjection>(ProjectionLifecycle.Async).
    [Fact]
    public void ConfigurarColaboradores_RegistraFichaColaboradorProjectionComoAsync()
    {
        using var provider = ProviderDeColaboradores();

        provider.GetRequiredService<IColaboradoresProjectionStore>()
            .AssertProyeccionAsyncRegistrada("FichaColaborador");
    }

    // Issue #356, mismo gotcha de "Numeric Revisioned Documents" que el issue #294 peno en dev y
    // que #328 ya cerro para TurnoVigente: Marten aplica ProjectionDocumentPolicy SOLO a los
    // documentos que son target de una proyeccion REGISTRADA en el store (UseNumericRevisions =
    // true, Metadata.Revision -- mt_version bigint -- habilitada, Metadata.Version -- mt_version
    // uuid -- deshabilitada). Si FichaColaboradorProjection dejara de registrarse arriba, este
    // mapping caeria al default y este test se pondria rojo.
    //
    // Este lado NO declara nada para que los valores sean asi: los impone Marten al registrar la
    // proyeccion (https://martendb.io/documents/concurrency, "Numeric Revisioned Documents";
    // Marten/Events/Projections/ProjectionDocumentPolicy.cs). O sea que este es el lado que DEFINE
    // la forma fisica de la tabla y el write-side el que debe replicarla -- por eso el oraculo se
    // congela aqui tambien: si una version futura de Marten cambiara el tipo por defecto de
    // Metadata.Revision, ambos lados se pondrian rojos juntos en vez de dejar que la divergencia
    // llegue a dev como un 42804 por request.
    //
    // Espejo de ComposicionServiciosTests (Colaboradores.Tests)
    // .AgregarServiciosColaboradores_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaFichaColaborador.
    [Fact]
    public void ConfigurarColaboradores_MaterializaFichaColaboradorConRevisionNumerica()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaColaborador));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Issue #356, mitad worker del par 2 de compatibilidad write-side/read-side (MEF-ADR-0034
    // seccion 6), replica de la guarda que #328 dejo para TurnoVigente: el daemon materializa
    // FichaColaborador desde este named store y el Function App de Colaboradores la lee, en otro
    // proceso, con session.LoadAsync. Que ambos lados converjan en la MISMA tabla fisica, la MISMA
    // tenancy y el MISMO IdMember no lo garantiza ningun compilador -- son dos configuraciones de
    // Marten independientes sobre el mismo schema, y una divergencia deja el GET en 404 permanente
    // con el daemon funcionando (fallo silencioso que solo veria el smoke test contra dev).
    //
    // Espejo de ComposicionServiciosTests (Colaboradores.Tests)
    // .AgregarServiciosColaboradores_ResuelveFichaColaboradorSobreLaTablaQueMaterializaElWorker_...,
    // sin que ningun ensamblado referencie al otro (CA-ADR-0029).
    [Fact]
    public void ConfigurarColaboradores_MaterializaFichaColaboradorSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaColaborador));

        mapping.TableName.QualifiedName.Should().Be("colaboradores.mt_doc_fichacolaborador");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(FichaColaborador.Id));
    }

    // Issue #357: segunda proyeccion concreta de Colaboradores -- la PRIMERA receta N2
    // (MultiStreamProjection<CategoriaDeEtiquetas, string>) de este BC: eventos EtiquetaAsignada de
    // MUCHOS streams de ColaboradorAggregateRoot convergen en el MISMO documento cuando comparten
    // categoria normalizada (skills/projections/modelos-marten.md). Mismo patron que #356 dejo para
    // FichaColaboradorProjection (N1): complementa ConfigurarColaboradores_NoRegistraNingunaProyeccionInline
    // -- aquella prueba que NADA quedo Inline, esta prueba que la proyeccion CONCRETA se registro con
    // lifecycle Async, el canonico del worker (MEF-ADR-0034 seccion 3). El seam
    // (ConfiguracionMartenProjectionsColaboradores.ConfigurarColaboradores) ya existe desde el issue
    // #360 y ya registra FichaColaboradorProjection (#356); este issue le agrega la unica linea
    // opts.Projections.Add<CategoriaDeEtiquetasProjection>(ProjectionLifecycle.Async) -- ausente hoy,
    // por eso este test queda en rojo hasta que projection-implementer la sume.
    [Fact]
    public void ConfigurarColaboradores_RegistraCategoriaDeEtiquetasProjectionComoAsync()
    {
        using var provider = ProviderDeColaboradores();

        provider.GetRequiredService<IColaboradoresProjectionStore>()
            .AssertProyeccionAsyncRegistrada("CategoriaDeEtiquetas");
    }

    // Issue #357, mismo gotcha de "Numeric Revisioned Documents" que #356 congelo para
    // FichaColaborador (ver el comentario de ConfigurarColaboradores_MaterializaFichaColaboradorCon
    // RevisionNumerica): ProjectionDocumentPolicy aplica POR DOCUMENTO target de una proyeccion
    // registrada, asi que la vista nueva necesita su propia guarda -- la de FichaColaborador no la
    // cubre. Este lado no declara nada: los valores los impone Marten al registrar
    // CategoriaDeEtiquetasProjection arriba, y por eso es el lado que DEFINE la forma fisica que el
    // Function App debe replicar.
    //
    // Espejo de ComposicionServiciosTests (Colaboradores.Tests)
    // .AgregarServiciosColaboradores_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaCategoriaDeEtiquetas.
    [Fact]
    public void ConfigurarColaboradores_MaterializaCategoriaDeEtiquetasConRevisionNumerica()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(CategoriaDeEtiquetas));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Issue #357, mitad worker del par 2 de compatibilidad write-side/read-side (MEF-ADR-0034
    // seccion 6) para la vista nueva: el daemon materializa CategoriaDeEtiquetas desde este named
    // store y el Function App de Colaboradores la lee, en otro proceso, con session.Query
    // (ListarCategoriasDeEtiquetas). Que ambos lados converjan en la MISMA tabla fisica, la MISMA
    // tenancy y el MISMO IdMember no lo garantiza ningun compilador.
    //
    // Espejo de ComposicionServiciosTests (Colaboradores.Tests)
    // .AgregarServiciosColaboradores_ResuelveCategoriaDeEtiquetasSobreLaTablaQueMaterializaElWorker_...,
    // sin que ningun ensamblado referencie al otro (CA-ADR-0029).
    [Fact]
    public void ConfigurarColaboradores_MaterializaCategoriaDeEtiquetasSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(CategoriaDeEtiquetas));

        mapping.TableName.QualifiedName.Should().Be("colaboradores.mt_doc_categoriadeetiquetas");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(CategoriaDeEtiquetas.Id));
    }

    // Issue #373 CA-5: indices declarados en el seam del worker para el listado QUERY de fichas
    // vigentes (ListarFichasColaborador, MEF-ADR-0042) -- rango sobre VigenteHasta, GIN sobre
    // EtiquetasNormalizadas (containment JSONB del filtro AND por etiquetas, precedente #337 sobre
    // Bloques/SedeId sobre TurnoVigente) y btree sobre NombreCompleto (orden del keyset). Ninguna
    // proyeccion ni read model nuevos: este issue solo suma indices sobre la MISMA
    // FichaColaborador ya registrada (#356).
    //
    // Baseline verificado por spike propio (sin este issue, StoreOptions.FindOrResolveDocumentType):
    // FichaColaborador no declara ningun indice propio hoy -- Indexes.Count == 0 --, asi que las
    // tres guardas de abajo fallan en la fase roja.
    //
    // Oraculo tolerante al MECANISMO exacto de Marten que elija projection-implementer (computed
    // Index(x => x.Campo) vs Duplicate(...).Index(...), DocumentIndex vs ComputedIndex): ambos
    // caminos agregan una entrada a IDocumentType.Indexes cuyo IndexDefinition expone Method y
    // Columns publicamente (Weasel.Postgresql.Tables), pero el nombre de indice y la forma exacta
    // del locator SQL varian segun el camino elegido -- por eso el assert busca el nombre del campo
    // C# (o su forma snake_case) como SUBCADENA de Columns, sin fijar el locator completo. Verificado
    // por spike propio: Schema.For<FichaColaborador>().Index(x => x.VigenteHasta) produce
    // Columns=["(public.mt_immutable_date(data ->> 'VigenteHasta'))"]; cualquier implementacion que
    // toque ese campo, por el camino que sea, deja el nombre del campo en alguna parte del locator.
    private static bool IndiceMencionaCampo(IndexDefinition indice, params string[] fragmentosDelCampo) =>
        indice.Columns is { } columnas
        && columnas.Any(columna => fragmentosDelCampo.All(
            fragmento => columna.Contains(fragmento, StringComparison.OrdinalIgnoreCase)));

    [Fact]
    public void ConfigurarColaboradores_DeclaraIndiceGinSobreEtiquetasNormalizadasDeFichaColaborador()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaColaborador));

        mapping.Indexes.Should().Contain(indice =>
            indice.Method == IndexMethod.gin && IndiceMencionaCampo(indice, "etiqueta"));
    }

    [Fact]
    public void ConfigurarColaboradores_DeclaraUnIndiceSobreVigenteHastaDeFichaColaborador()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaColaborador));

        mapping.Indexes.Should().Contain(indice => IndiceMencionaCampo(indice, "vigente", "hasta"));
    }

    [Fact]
    public void ConfigurarColaboradores_DeclaraUnIndiceSobreNombreCompletoDeFichaColaborador()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaColaborador));

        mapping.Indexes.Should().Contain(indice => IndiceMencionaCampo(indice, "nombre", "completo"));
    }

    // --- Sedes (issue #455: el named store nace con el andamiaje del dominio, sin proyecciones) ---

    // Mismas guardas que los demas dominios: son las que fijan la forma del named store ANTES de
    // que exista la primera proyeccion. Su valor esta justo ahi -- schema, identidad de stream,
    // tenancy y naming de eventos deben coincidir con el write-side desde el primer evento
    // persistido; descubrir una divergencia despues obliga a migrar datos ya escritos.
    //
    // Issue #461: el desglose #456-#460 (ya en main al momento de este issue) pobló
    // IdentidadEventosSedes.TiposPersistidos con los 9 tipos de SedeAggregateRoot -- ya no aplica
    // "el dominio no tiene ningun evento persistido" (nota original del issue #455). Sin ningun
    // value object con ctor privado (todos los eventos de Sedes son records planos): no hay
    // equivalente de ConfigurarX_ConservaElResolverDeSerializacionCustom para este dominio.

    [Fact]
    public void ConfigurarSedes_ResuelveElNamedStoreDelDominio()
    {
        using var provider = ProviderDeSedes();

        var store = provider.GetService<ISedesProjectionStore>();

        store.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurarSedes_RegistraElNamedStoreSobreElSchemaDeSedes()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>().AssertSchema("sedes");
    }

    [Fact]
    public void ConfigurarSedes_ReplicaLaMetadataDeEventoDelWriteSide()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>().AssertOpcionesDeEvento();
    }

    [Fact]
    public void ConfigurarSedes_NoRegistraNingunaProyeccionInline()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>().AssertSinProyeccionesInline();
    }

    [Fact]
    public void ConfigurarSedes_EnciendeElDaemonEnModoHotCold()
    {
        using var provider = ProviderDeSedes();

        provider.AssertDaemonHotCold<ISedesProjectionStore>();
    }

    // El stream de Sede se identifica por CodigoSede (texto del cliente), no por Guid: sin esta
    // guarda Marten volveria a su default AsGuid y el daemon leeria stream_id varchar como uuid,
    // sin encontrar ningun stream (issue #253).
    [Fact]
    public void ConfigurarSedes_DeclaraLaStreamIdentityComoString()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>().AssertStreamIdentityAsString();
    }

    [Fact]
    public void ConfigurarSedes_DeclaraTenancyDeEventosConjoined()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>().AssertTenancyDeEventosConjoined();
    }

    [Fact]
    public void ConfigurarSedes_DeclaraEventNamingStyleSmarterTypeName()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>().AssertEventNamingStyleSmarterTypeName();
    }

    [Fact]
    public void ConfigurarSedes_DeclaraLosDocumentosComoMultiTenant()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>()
            .AssertDocumentosMultiTenant<DocumentoCanarioTenancy>();
    }

    // Issue #461 (defensa en profundidad read-side, patron de Programacion/ControlHoras/
    // Colaboradores, issue #277): el dominio estrena su primera proyeccion concreta --
    // IdentidadEventosSedes.TiposPersistidos paso de vacia (issue #455) a los 9 tipos de
    // SedeAggregateRoot (issues #456-#460).
    //
    // Tipos esperados listados literalmente (oraculo independiente, MEF-ADR-0002): leerlos de
    // IdentidadEventosSedes.TiposPersistidos acoplaria este guardrail al mismo artefacto que
    // AliasEventosSedesTests ya verifica en el write-side.
    [Fact]
    public void ConfigurarSedes_RegistraLosTiposDeEventoPersistidos()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>()
            .AssertEventosPersistidosRegistrados(
            [
                typeof(SedeRegistrada), typeof(NombreSedeModificado), typeof(UbicacionActualizada),
                typeof(CentroDeCostosAsignado), typeof(CentroDeCostosRetirado),
                typeof(SedeActivada), typeof(SedeDesactivada),
                typeof(DispositivoInstalado), typeof(DispositivoRetirado)
            ]);
    }

    // Issue #461 CA-1..CA-4: primera proyeccion concreta del dominio Sedes (N1,
    // SingleStreamProjection<FichaSede, string> sobre el stream de SedeAggregateRoot). Complementa
    // ConfigurarSedes_NoRegistraNingunaProyeccionInline: aquella prueba que NADA quedo Inline, esta
    // prueba que la proyeccion CONCRETA se registro con lifecycle Async, el canonico del worker
    // (MEF-ADR-0034 seccion 3). El seam (ConfiguracionMartenProjectionsSedes.ConfigurarSedes) existe
    // desde el issue #455 sin ninguna proyeccion; este issue le agrega la unica linea
    // opts.Projections.Add<FichaSedeProjection>(ProjectionLifecycle.Async).
    [Fact]
    public void ConfigurarSedes_RegistraFichaSedeProjectionComoAsync()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>()
            .AssertProyeccionAsyncRegistrada("FichaSede");
    }

    // Issue #461, mismo gotcha de "Numeric Revisioned Documents" que el issue #294 peno en dev y que
    // #328/#356 ya cerraron para TurnoVigente/FichaColaborador: Marten aplica ProjectionDocumentPolicy
    // SOLO a los documentos target de una proyeccion REGISTRADA en el store (UseNumericRevisions =
    // true, Metadata.Revision -- mt_version bigint -- habilitada, Metadata.Version -- mt_version uuid
    // -- deshabilitada). Si FichaSedeProjection dejara de registrarse arriba, este mapping caeria al
    // default y este test se pondria rojo.
    //
    // Este lado NO declara nada para que los valores sean asi: los impone Marten al registrar la
    // proyeccion. O sea que este es el lado que DEFINE la forma fisica de la tabla y el write-side el
    // que debe replicarla -- por eso el oraculo se congela aqui tambien: si una version futura de
    // Marten cambiara el tipo por defecto de Metadata.Revision, ambos lados se pondrian rojos juntos
    // en vez de dejar que la divergencia llegue a dev como un 42804 por request.
    //
    // Espejo de ComposicionServiciosTests (Sedes.Tests)
    // .AgregarServiciosSedes_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaFichaSede.
    [Fact]
    public void ConfigurarSedes_MaterializaFichaSedeConRevisionNumerica()
    {
        using var provider = ProviderDeSedes();

        var mapping = provider.GetRequiredService<ISedesProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaSede));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Issue #461, mitad worker del par 2 de compatibilidad write-side/read-side (MEF-ADR-0034
    // seccion 6): el daemon materializa FichaSede desde este named store y el Function App de Sedes
    // la lee, en otro proceso, con session.LoadAsync/Query. Que ambos lados converjan en la MISMA
    // tabla fisica, la MISMA tenancy y el MISMO IdMember no lo garantiza ningun compilador, y una
    // divergencia deja el GET en 404 permanente con el daemon funcionando.
    //
    // Espejo de ComposicionServiciosTests (Sedes.Tests)
    // .AgregarServiciosSedes_ResuelveFichaSedeSobreLaTablaQueMaterializaElWorker_..., sin que ningun
    // ensamblado referencie al otro (CA-ADR-0029).
    [Fact]
    public void ConfigurarSedes_MaterializaFichaSedeSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeSedes();

        var mapping = provider.GetRequiredService<ISedesProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(FichaSede));

        mapping.TableName.QualifiedName.Should().Be("sedes.mt_doc_fichasede");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(FichaSede.Id));
    }

    // Complementa ConfigurarSedes_NoRegistraNingunaProyeccionInline: aquella prueba que NADA quedo
    // Inline, esta que la proyeccion CONCRETA quedo registrada con lifecycle Async, el canonico del
    // worker (MEF-ADR-0034 seccion 3). El nombre esperado es el de la VISTA, no el de la clase de
    // proyeccion (Marten nombra la proyeccion por su documento target).
    //
    [Fact]
    public void ConfigurarSedes_RegistraUbicacionDispositivoProjectionComoAsync()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>()
            .AssertProyeccionAsyncRegistrada("UbicacionDispositivo");
    }

    // Issue #467: el par espejo de mt_version que este archivo dejaba anotado como pendiente ya
    // aplica -- el Function App de Sedes consulta UbicacionDispositivo desde la reaccion
    // ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado. Mismo razonamiento que FichaSede
    // arriba: este lado NO declara nada, la forma la impone Marten al registrar la proyeccion.
    //
    // Espejo de ComposicionServiciosTests (Sedes.Tests)
    // .AgregarServiciosSedes_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaUbicacionDispositivo.
    [Fact]
    public void ConfigurarSedes_MaterializaUbicacionDispositivoConRevisionNumerica()
    {
        using var provider = ProviderDeSedes();

        var mapping = provider.GetRequiredService<ISedesProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(UbicacionDispositivo));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Segunda dimension del par 2 para UbicacionDispositivo (tabla, tenancy, IdMember).
    //
    // Espejo de ComposicionServiciosTests (Sedes.Tests)
    // .AgregarServiciosSedes_ResuelveUbicacionDispositivoSobreLaTablaQueMaterializaElWorker_...
    [Fact]
    public void ConfigurarSedes_MaterializaUbicacionDispositivoSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeSedes();

        var mapping = provider.GetRequiredService<ISedesProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(UbicacionDispositivo));

        mapping.TableName.QualifiedName.Should().Be("sedes.mt_doc_ubicaciondispositivo");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(UbicacionDispositivo.Id));
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
        provider.GetService<IColaboradoresProjectionStore>().Should().NotBeNull();
        provider.GetService<ISedesProjectionStore>().Should().NotBeNull();
    }
}
