using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;
using Bitakora.ControlAsistencia.ReadModels;
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
// resuelve del contenedor, apunta al schema del write-side, replica su metadata de evento, su
// identidad de stream, su tenancy, su naming de eventos, la politica de documentos multi-tenant y el
// resolver de serializacion custom (fuente unica con el write-side), no tiene ninguna proyeccion
// Inline y corre su daemon en HotCold. La superficie de Marten que cada una interroga vive en
// AssertsProyecciones.
public class ConfiguracionMartenProjectionsTests
{
    private const string ConnectionStringDummy = "Host=localhost;Database=dummy";

    // Identidad del canario de round-trip de Programacion (el de ControlHoras, MarcacionRegistrada, se
    // identifica por CodigoColaborador string). El valor es irrelevante: solo debe ser estable.
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

    [Fact]
    public void ConfigurarProgramacion_DeclaraTenancyDeEventosConjoined()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertTenancyDeEventosConjoined();
    }

    [Fact]
    public void ConfigurarProgramacion_DeclaraEventNamingStyleSmarterTypeName()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertEventNamingStyleSmarterTypeName();
    }

    // Policies.AllDocumentsAreMultiTenanted() gobierna el par 2 (worker -> query-side): la forma de la
    // tabla de cualquier read model futuro de este dominio.
    [Fact]
    public void ConfigurarProgramacion_DeclaraLosDocumentosComoMultiTenant()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertDocumentosMultiTenant<DocumentoCanarioTenancy>();
    }

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
            .Should().Be("06:00-14:00, descanso 10:00-10:15, extra 06:00-06:30");
    }

    // Evita que el daemon lea streams preexistentes sin el tipo registrado en su propio EventGraph.
    //
    // Tipos esperados listados literalmente (oraculo independiente, MEF-ADR-0002): leerlos de
    // IdentidadEventosProgramacion.TiposPersistidos acoplaria este guardrail al mismo artefacto que
    // IdentidadEventosProgramacionTests ya verifica en el write-side. Debe seguir siendo espejo del
    // oraculo del write-side (ComposicionServiciosTests
    // .AgregarServiciosProgramacion_RegistraLosTiposDeEventoPersistidos_...): un tipo listado alli
    // y no aqui es exactamente la divergencia que este guardrail existe para cazar.
    [Fact]
    public void ConfigurarProgramacion_RegistraLosTiposDeEventoPersistidos()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertEventosPersistidosRegistrados(
            [
                typeof(TurnoCreado),
                typeof(ProgramacionTurnoSolicitada),
                typeof(TurnoRetirado),
                typeof(CancelacionProgramacionSolicitada),
                typeof(FranjaAgregada),
                typeof(DescansoAgregado),
                typeof(ExtraAgregado),
                typeof(FranjaQuitada),
                typeof(DescansoQuitado),
                typeof(ExtraQuitado),
                typeof(SedeDeFranjaAsignada),
                typeof(SedeDeFranjaRetirada),
                typeof(PlantillaDeTurnosCreada),
                typeof(DiaDePlantillaDeTurnosAsignado),
                typeof(PlantillaDeTurnosRetirada),
                typeof(VentanaDeEntradaModificada)
            ]);
    }

    // Complementa ConfigurarProgramacion_NoRegistraNingunaProyeccionInline: aquella prueba
    // que NADA quedo Inline -- una lista vacia la pasaria --, esta que la proyeccion CONCRETA se
    // registro con lifecycle Async (MEF-ADR-0034 seccion 3).
    [Fact]
    public void ConfigurarProgramacion_RegistraFichaTurnoProjectionComoAsync()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertProyeccionAsyncRegistrada("FichaTurno");
    }

    // Marten aplica ProjectionDocumentPolicy ("Numeric Revisioned Documents") SOLO a los documentos
    // target de una proyeccion REGISTRADA: mt_version bigint. Este lado no declara nada para que sea
    // asi -- lo impone Marten --, pero DEFINE la forma fisica de la tabla que el write-side debe
    // replicar, por eso el oraculo se congela aqui tambien. Si FichaTurnoProjection dejara de
    // registrarse arriba, el mapping caeria al default y este test se pondria rojo.
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

    // Mitad worker del par 2 (MEF-ADR-0034 seccion 6): el daemon materializa FichaTurno desde este
    // named store y el Function App la lee en otro proceso. Que ambos converjan en la MISMA tabla,
    // tenancy e IdMember no lo garantiza ningun compilador -- una divergencia deja el GET en 404
    // permanente con el daemon funcionando.
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

    [Fact]
    public void ConfigurarProgramacion_RegistraLimitesDeJornadaProjectionComoAsync()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertProyeccionAsyncRegistrada("LimitesDeJornada");
    }

    [Fact]
    public void ConfigurarProgramacion_MaterializaLimitesDeJornadaConRevisionNumerica()
    {
        using var provider = ProviderDeProgramacion();

        var mapping = provider.GetRequiredService<IProgramacionProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(LimitesDeJornada));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ConfigurarProgramacion_MaterializaLimitesDeJornadaSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeProgramacion();

        var mapping = provider.GetRequiredService<IProgramacionProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(LimitesDeJornada));

        mapping.TableName.QualifiedName.Should().Be("programacion.mt_doc_limitesdejornada");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(LimitesDeJornada.Id));
    }

    [Fact]
    public void ConfigurarProgramacion_RegistraAusenciaVigenteProjectionComoAsync()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertProyeccionAsyncRegistrada("AusenciaVigente");
    }

    // Mitad worker del par 2 para AusenciaVigente (MEF-ADR-0034 seccion 6), espejo de
    // AgregarServiciosProgramacion_*_ParaAusenciaVigente en Programacion.Tests.
    [Fact]
    public void ConfigurarProgramacion_MaterializaAusenciaVigenteConRevisionNumerica()
    {
        using var provider = ProviderDeProgramacion();

        var mapping = provider.GetRequiredService<IProgramacionProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(AusenciaVigente));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ConfigurarProgramacion_MaterializaAusenciaVigenteSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeProgramacion();

        var mapping = provider.GetRequiredService<IProgramacionProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(AusenciaVigente));

        mapping.TableName.QualifiedName.Should().Be("programacion.mt_doc_ausenciavigente");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(AusenciaVigente.Id));
    }

    [Fact]
    public void ConfigurarProgramacion_RegistraCuadroDeTurnosProjectionComoAsync()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertProyeccionAsyncRegistrada("CuadroDeTurnos");
    }

    [Fact]
    public void ConfigurarProgramacion_MaterializaCuadroDeTurnosSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeProgramacion();

        var mapping = provider.GetRequiredService<IProgramacionProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(CuadroDeTurnos));

        mapping.TableName.QualifiedName.Should().Be("programacion.mt_doc_cuadrodeturnos");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(CuadroDeTurnos.Id));
        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
    }

    [Fact]
    public void ConfigurarProgramacion_FijaElSaltoDeHuecosEstancadosEnCincoMinutos()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertSaltoDeHuecosEstancadosEnCincoMinutos();
    }

    [Fact]
    public void ConfigurarControlHoras_FijaElSaltoDeHuecosEstancadosEnCincoMinutos()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertSaltoDeHuecosEstancadosEnCincoMinutos();
    }

    [Fact]
    public void ConfigurarColaboradores_FijaElSaltoDeHuecosEstancadosEnCincoMinutos()
    {
        using var provider = ProviderDeColaboradores();

        provider.GetRequiredService<IColaboradoresProjectionStore>().AssertSaltoDeHuecosEstancadosEnCincoMinutos();
    }

    [Fact]
    public void ConfigurarSedes_FijaElSaltoDeHuecosEstancadosEnCincoMinutos()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>().AssertSaltoDeHuecosEstancadosEnCincoMinutos();
    }

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

    [Fact]
    public void ConfigurarControlHoras_DeclaraTenancyDeEventosConjoined()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertTenancyDeEventosConjoined();
    }

    [Fact]
    public void ConfigurarControlHoras_DeclaraEventNamingStyleSmarterTypeName()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertEventNamingStyleSmarterTypeName();
    }

    // Policies.AllDocumentsAreMultiTenanted() gobierna el par 2 (worker -> query-side): la forma de la
    // tabla de cualquier read model futuro de este dominio.
    [Fact]
    public void ConfigurarControlHoras_DeclaraLosDocumentosComoMultiTenant()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertDocumentosMultiTenant<DocumentoCanarioTenancy>();
    }

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

    // Evita que el daemon lea streams preexistentes sin el tipo registrado en su propio EventGraph.
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
                typeof(DepuracionDiaRecibida),
                typeof(CoberturaVecinaActualizada),
                typeof(CierreDeLaVisperaActualizado)
            ]);
    }

    [Fact]
    public void ConfigurarControlHoras_RegistraTurnoVigenteProjectionComoAsync()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertProyeccionAsyncRegistrada("TurnoVigente");
    }

    [Fact]
    public void ConfigurarControlHoras_RegistraAdvertenciasProgramacionSemanalProjectionComoAsync()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>()
            .AssertProyeccionAsyncRegistrada("AdvertenciasProgramacionSemanal");
    }

    // Mitad worker del par espejo (mt_version y tabla/tenancy/id, MEF-ADR-0034 seccion 6). La mitad
    // write-side la congelan en ComposicionServiciosTests los tests espejo
    // ...EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaAdvertenciasProgramacionSemanal y
    // ...ResuelveAdvertenciasProgramacionSemanalSobreLaTablaQueMaterializaElWorker_CuandoElContenedorEstaCompuesto.
    [Fact]
    public void ConfigurarControlHoras_MaterializaAdvertenciasProgramacionSemanalConRevisionNumerica()
    {
        using var provider = ProviderDeControlHoras();

        var mapping = provider.GetRequiredService<IControlHorasProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(AdvertenciasProgramacionSemanal));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ConfigurarControlHoras_MaterializaAdvertenciasProgramacionSemanalSobreLaTablaQueConsultaraElWriteSide()
    {
        using var provider = ProviderDeControlHoras();

        var mapping = provider.GetRequiredService<IControlHorasProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(AdvertenciasProgramacionSemanal));

        mapping.TableName.QualifiedName.Should().Be("control_horas.mt_doc_advertenciasprogramacionsemanal");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(AdvertenciasProgramacionSemanal.Id));
    }

    // Marten aplica ProjectionDocumentPolicy SOLO a los documentos que son target de una proyeccion
    // REGISTRADA en el store (UseNumericRevisions = true, Metadata.Revision -- mt_version bigint --
    // habilitada, Metadata.Version -- mt_version uuid -- deshabilitada). Si TurnoVigenteProjection
    // dejara de registrarse arriba, este mapping caeria al default (Version habilitado, Revision
    // deshabilitado) y este test se pondria rojo.
    //
    // Este lado NO declara nada para que los valores sean asi: los impone Marten al registrar la
    // proyeccion (https://martendb.io/documents/concurrency, "Numeric Revisioned Documents";
    // Marten/Events/Projections/ProjectionDocumentPolicy.cs). Este es el lado que DEFINE la forma
    // fisica de la tabla y el write-side el que debe replicarla: si una version futura de Marten
    // cambiara el tipo por defecto de Metadata.Revision, ambos lados se pondrian rojos juntos en vez
    // de dejar que la divergencia llegue a dev como un 42804 por request.
    //
    // Espejo literal de ComposicionServiciosTests
    // .AgregarServiciosControlHoras_EsperaLaMismaColumnaDeVersionQueMaterializaraElWorker_ParaTurnoVigente.
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

    // Mitad worker del par 2 (MEF-ADR-0034 seccion 6): el daemon materializa TurnoVigente desde este
    // named store y el Function App de ControlHoras la lee, en otro proceso, con session.LoadAsync.
    // Que ambos converjan en la MISMA tabla fisica, tenancy e IdMember no lo garantiza ningun
    // compilador: una divergencia deja el GET en 404 permanente con el daemon funcionando. Marten
    // resuelve los tres valores por convencion, pero el write-side declara su propio
    // Schema.For<TurnoVigente>(): ComposicionServiciosTests
    // .AgregarServiciosControlHoras_ResuelveTurnoVigenteSobreLaTablaQueMaterializaElWorker... congela
    // el MISMO literal, sin que ningun ensamblado referencie al otro.
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

    // ColaboradorRegistrado lleva payload rico -- Identificacion y NombreColaborador son sealed class
    // con campos privados y ConfigurarSerializacion --, asi que el write-side instala un
    // TypeInfoResolver custom dentro de su ConfigureMarten. Sin la MISMA fuente en este named store, el
    // daemon leeria colaborador_registrado con STJ vanilla y reventaria con NotSupportedException en
    // runtime, no en el build. Se invoca la misma clase, nunca una copia.
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
        restaurado.Identificacion.ToString().Should().Be("CC-79543210");
        restaurado.Nombre.NombreCompleto.Should().Be("Luis Augusto Barreto Prieto");
    }

    [Fact]
    public void ConfigurarColaboradores_RegistraFichaColaboradorProjectionComoAsync()
    {
        using var provider = ProviderDeColaboradores();

        provider.GetRequiredService<IColaboradoresProjectionStore>()
            .AssertProyeccionAsyncRegistrada("FichaColaborador");
    }

    // Mismo gotcha de "Numeric Revisioned Documents" que documenta
    // ConfigurarControlHoras_MaterializaTurnoVigenteConRevisionNumerica.
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

    // Mitad worker del par 2 para FichaColaborador, misma guarda que la de TurnoVigente: el Function
    // App de Colaboradores la lee con session.LoadAsync.
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

    [Fact]
    public void ConfigurarColaboradores_RegistraCategoriaDeEtiquetasProjectionComoAsync()
    {
        using var provider = ProviderDeColaboradores();

        provider.GetRequiredService<IColaboradoresProjectionStore>()
            .AssertProyeccionAsyncRegistrada("CategoriaDeEtiquetas");
    }

    // Mismo gotcha de "Numeric Revisioned Documents" (ver
    // ConfigurarControlHoras_MaterializaTurnoVigenteConRevisionNumerica): ProjectionDocumentPolicy
    // aplica POR DOCUMENTO target de una proyeccion registrada, asi que cada vista necesita su propia
    // guarda.
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

    // Mitad worker del par 2 para CategoriaDeEtiquetas: el Function App de Colaboradores la lee con
    // session.Query (ListarCategoriasDeEtiquetas).
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

    // Indices del seam del worker para el listado QUERY de fichas vigentes (ListarFichasColaborador):
    // rango sobre VigenteHasta, GIN sobre EtiquetasNormalizadas (containment JSONB del filtro AND por
    // etiquetas) y btree sobre NombreCompleto (orden del keyset).
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

    [Fact]
    public void ConfigurarColaboradores_RegistraDirectorioColaboradorProjectionComoAsync()
    {
        using var provider = ProviderDeColaboradores();

        provider.GetRequiredService<IColaboradoresProjectionStore>()
            .AssertProyeccionAsyncRegistrada("DirectorioColaborador");
    }

    // Marten aplica ProjectionDocumentPolicy (mt_version bigint) SOLO a los documentos target de
    // una proyeccion REGISTRADA en el store: si DirectorioColaboradorProjection dejara de
    // registrarse arriba, este mapping caeria al default y este test se pondria rojo.
    [Fact]
    public void ConfigurarColaboradores_MaterializaDirectorioColaboradorConRevisionNumerica()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(DirectorioColaborador));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    // Mitad worker del par espejo write-side/read-side (MEF-ADR-0034 seccion 6): congela el
    // literal de tabla/tenancy/id que el Function App debera replicar cuando consulte esta vista --
    // la otra mitad nace con el endpoint que la lea.
    [Fact]
    public void ConfigurarColaboradores_MaterializaDirectorioColaboradorSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(DirectorioColaborador));

        mapping.TableName.QualifiedName.Should().Be("colaboradores.mt_doc_directoriocolaborador");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(DirectorioColaborador.Id));
    }

    [Fact]
    public void ConfigurarColaboradores_DeclaraUnIndiceSobreNumeroDocumentoDeDirectorioColaborador()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(DirectorioColaborador));

        mapping.Indexes.Should().Contain(indice => IndiceMencionaCampo(indice, "numero", "documento"));
    }

    [Fact]
    public void ConfigurarColaboradores_DeclaraIndiceGinSobreTokensNombreDeDirectorioColaborador()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(DirectorioColaborador));

        mapping.Indexes.Should().Contain(indice =>
            indice.Method == IndexMethod.gin && IndiceMencionaCampo(indice, "tokens", "nombre"));
    }

    [Fact]
    public void ConfigurarColaboradores_DeclaraUnIndiceSobreNombreCompletoDeDirectorioColaborador()
    {
        using var provider = ProviderDeColaboradores();

        var mapping = provider.GetRequiredService<IColaboradoresProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(DirectorioColaborador));

        mapping.Indexes.Should().Contain(indice => IndiceMencionaCampo(indice, "nombre", "completo"));
    }

    // Schema, identidad de stream, tenancy y naming de eventos deben coincidir con el write-side desde
    // el primer evento persistido; descubrir una divergencia despues obliga a migrar datos ya
    // escritos. Los eventos de Sedes son records planos, sin value object con ctor privado: no hay
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
    // sin encontrar ningun stream.
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

    [Fact]
    public void ConfigurarSedes_RegistraFichaSedeProjectionComoAsync()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>()
            .AssertProyeccionAsyncRegistrada("FichaSede");
    }

    // Mismo gotcha de "Numeric Revisioned Documents" que documenta
    // ConfigurarControlHoras_MaterializaTurnoVigenteConRevisionNumerica.
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

    // Mitad worker del par 2 para FichaSede, misma guarda que la de TurnoVigente: el Function App de
    // Sedes la lee con session.LoadAsync/Query.
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

    // El nombre esperado es el de la VISTA, no el de la clase de proyeccion (Marten nombra la
    // proyeccion por su documento target).
    [Fact]
    public void ConfigurarSedes_RegistraUbicacionDispositivoProjectionComoAsync()
    {
        using var provider = ProviderDeSedes();

        provider.GetRequiredService<ISedesProjectionStore>()
            .AssertProyeccionAsyncRegistrada("UbicacionDispositivo");
    }

    // El Function App de Sedes consulta UbicacionDispositivo desde la reaccion
    // ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado. Mismo razonamiento que FichaSede: este
    // lado NO declara nada, la forma la impone Marten al registrar la proyeccion.
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

    [Fact]
    public void ConfigurarProgramacion_RegistraBorradorDeProgramacionProjectionComoAsync()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>()
            .AssertProyeccionAsyncRegistrada("BorradorDeProgramacion");
    }

    [Fact]
    public void ConfigurarProgramacion_MaterializaBorradorDeProgramacionConRevisionNumerica()
    {
        using var provider = ProviderDeProgramacion();

        var mapping = provider.GetRequiredService<IProgramacionProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(BorradorDeProgramacion));

        mapping.Metadata.Revision.Enabled.Should().BeTrue();
        mapping.Metadata.Revision.Type.Should().Be("bigint");
        mapping.Metadata.Version.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ConfigurarProgramacion_MaterializaBorradorDeProgramacionSobreLaTablaQueConsultaElWriteSide()
    {
        using var provider = ProviderDeProgramacion();

        var mapping = provider.GetRequiredService<IProgramacionProjectionStore>()
            .Options.FindOrResolveDocumentType(typeof(BorradorDeProgramacion));

        mapping.TableName.QualifiedName.Should().Be("programacion.mt_doc_borradordeprogramacion");
        mapping.TenancyStyle.Should().Be(TenancyStyle.Conjoined);
        mapping.IdMember.Name.Should().Be(nameof(BorradorDeProgramacion.Id));
    }

    [Fact]
    public void BorradorDeProgramacion_LlevaLaMarcaMaterializadoPorProyeccion()
    {
        typeof(BorradorDeProgramacion).GetCustomAttributes(typeof(MaterializadoPorProyeccionAttribute), false)
            .Should().NotBeEmpty();
    }
}
