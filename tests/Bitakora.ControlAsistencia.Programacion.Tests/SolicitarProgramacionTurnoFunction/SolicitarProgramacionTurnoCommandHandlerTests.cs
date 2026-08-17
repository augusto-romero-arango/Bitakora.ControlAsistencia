// HU-10: Solicitar programacion de turno del catalogo

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;
using Bitakora.ControlAsistencia.PublicEvents.Colaboradores;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoCommandHandlerTests
    : CommandHandlerAsyncTest<SolicitarProgramacionTurno>
{
    // --- Constantes de prueba ---
    private static readonly Guid TurnoId =
        Guid.Parse("018e4c1a-4f2b-7000-8000-aabbccddeeff");
    private static readonly Guid TurnoConHijasId =
        Guid.Parse("018e4c1a-4f2b-7000-8000-112233445566");
    private static readonly Guid TurnoConSedePrearmadaId =
        Guid.Parse("018e4c1a-4f2b-7000-8000-778899aabbcc");
    private static readonly Guid TurnoConFranjasMixtasId =
        Guid.Parse("018e4c1a-4f2b-7000-8000-1a2b3c4d5e6f");
    private static readonly DateOnly Fecha1 = new(2026, 4, 7);
    private static readonly DateOnly Fecha2 = new(2026, 4, 8);

    private static readonly InformacionColaborador Colaborador =
        new("E001", "CC", "12345678", "Juan", "Perez");

    // Mismo colaborador, en la forma que el handler debe producir para el evento privado
    // (CA-ADR-0029 decision #5): si el mapeo pierde o permuta un campo, estos tests lo delatan.
    private static readonly DetalleColaborador ColaboradorDetalle =
        new("E001", "CC", "12345678", "Juan", "Perez");

    // Issue #319 CA-2/CA-5: mismo colaborador, en el record propio de Programacion.DomainEvents que
    // ahora tipa ProgramacionTurnoSolicitada.Colaborador (tres islas, MEF-ADR-0039 decision 2).
    private static readonly ColaboradorProgramado ColaboradorProgramadoEsperado =
        new("E001", "CC", "12345678", "Juan", "Perez");

    // El DetalleTurno esperado corresponde al catalogo creado en CrearEventoTurno(). Forma de BUS
    // (PrivateEvents) -- solo se usa en ThenIsPublishedPrivately (CA-5: unico punto de mapeo).
    // Issue #288 CA-2: Descripcion lleva el texto real que produce el ToString() del tipo rico
    // (CatalogoTurnos a nivel turno, FranjaOrdinaria a nivel franja). La coherencia entre ambos
    // la prueban CatalogoTurnosTests y FranjaOrdinariaToDetalleTests; aqui el valor literal
    // documenta que fluye intacto hasta el evento emitido y el publicado por el bus privado.
    private static readonly DetalleTurno DetalleEsperado = new(
        "Turno Manana",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)")
        }.AsReadOnly(),
        "Turno Manana (06:00-14:00)");

    // Issue #319 CA-1/CA-5: mismo turno, en el record propio del dominio (Programacion.DomainEvents)
    // que ahora tipa el evento persistido ProgramacionTurnoSolicitada.DetalleTurno.
    private static readonly TurnoProgramado TurnoProgramadoEsperado = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)")
        }.AsReadOnly(),
        "Turno Manana (06:00-14:00)");

    // --- Configuracion del handler ---

    protected override ICommandHandlerAsync<SolicitarProgramacionTurno> Handler =>
        new SolicitarProgramacionTurnoCommandHandler(EventStore, PrivateEventSender);

    // --- Factory methods ---

    private static TurnoCreado CrearEventoTurno() =>
        TurnoCreado.Crear(
            TurnoId,
            "Turno Manana",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    // Turno CON descansos y extras: es el unico camino que ejercita el nivel mas interno del mapeo
    // que el issue #319 introdujo en el handler (SubFranjaProgramada -> DetalleSubFranja) y la
    // recursion de las dos listas en MapearFranja. Con el turno sin hijas de arriba esos dos mapeos
    // no se ejecutan nunca, asi que perder la Descripcion de una sub-franja o permutar las listas
    // Descansos/Extras pasaba en verde -- verificado por mutacion en la revision de este PR.
    private static TurnoCreado CrearEventoTurnoConHijas() =>
        TurnoCreado.Crear(
            TurnoConHijasId,
            "Turno Partido",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0),
                [(new TimeOnly(10, 0), new TimeOnly(10, 15))],
                [(new TimeOnly(13, 0), new TimeOnly(14, 0))])]);

    // --- Formas esperadas del turno con hijas, en los dos roles de payload (CA-1, CA-5) ---
    //
    // Los literales de Descripcion son los que producen los ToString() de los tipos ricos
    // (SubFranja, FranjaOrdinaria, CatalogoTurnos); su coherencia la prueban FranjaOrdinariaToDetalleTests
    // y CatalogoTurnosTests, aqui documentan que fluyen intactos por AMBOS mapeos.

    private const string DescripcionDescanso = "(10:00-10:15)";
    private const string DescripcionExtra = "(13:00-14:00)";
    private const string DescripcionFranjaConHijas =
        "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-14:00)]";
    private const string DescripcionTurnoConHijas =
        "Turno Partido (06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-14:00)]";

    // Issue #331: sede efectiva del dia, en los dos roles de payload (evento persistido y evento
    // que cruza el bus interno) -- gemelos deliberados con paridad de campos (CA-ADR-0029/MEF-ADR-0039).
    private static readonly SedeProgramada SedePrincipal = new("SEDE-01", "Sede Principal");
    private static readonly DetalleSede SedePrincipalDetalle = new("SEDE-01", "Sede Principal");

    private static readonly TurnoProgramado TurnoConHijasProgramadoEsperado = new(
        "Turno Partido",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, DescripcionDescanso)],
                [new SubFranjaProgramada(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, DescripcionExtra)],
                DescripcionFranjaConHijas)
        }.AsReadOnly(),
        DescripcionTurnoConHijas);

    private static readonly DetalleTurno DetalleConHijasEsperado = new(
        "Turno Partido",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [new DetalleSubFranja(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, DescripcionDescanso)],
                [new DetalleSubFranja(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, DescripcionExtra)],
                DescripcionFranjaConHijas)
        }.AsReadOnly(),
        DescripcionTurnoConHijas);

    // --- Issue #335: turno del catalogo con la sede PREARMADA por franja ---
    //
    // No la trae la solicitud (esa es la sede de #331, que aqui va en null): la trae el turno,
    // desde que se creo, y llega por el snapshot existente (CatalogoTurnos.ObtenerDetalle ->
    // FranjaOrdinaria.ToDetalle). Desde #341 la cascada la atraviesa -- con la solicitud sin sede
    // es identidad -- y el handler la propaga tambien al payload de bus (MapearFranja).
    private static readonly SedeProgramada SedeSuba = new("SEDE-SUBA", "Suba");
    private static readonly DetalleSede SedeSubaDetalle = new("SEDE-SUBA", "Suba");

    private static TurnoCreado CrearEventoTurnoConSedePrearmada() =>
        TurnoCreado.Crear(
            TurnoConSedePrearmadaId,
            "Turno Con Sede",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], SedeSuba)]);

    // La sede entra al ToString() de la franja, asi que la Descripcion derivada la incluye.
    private static readonly string DescripcionFranjaConSede =
        $"(06:00-14:00)[{FranjaOrdinaria.Mensajes.LabelSede}:Suba]";
    private static readonly string DescripcionTurnoConSede =
        $"Turno Con Sede {DescripcionFranjaConSede}";

    private static readonly TurnoProgramado TurnoConSedePrearmadaEsperado = new(
        "Turno Con Sede",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], DescripcionFranjaConSede, SedeSuba)
        }.AsReadOnly(),
        DescripcionTurnoConSede);

    // Issue #341 CA-2: la solicitud va sin sede (null), asi que la cascada (franja.Sede ??
    // sedePorDefecto) deja la sede prearmada del catalogo intacta -- ahora tambien en el payload de
    // bus, que gana su propio campo Sede (DetalleFranjaOrdinaria, #341). Antes de este issue
    // DetalleFranjaOrdinaria no tenia campo Sede: el UNICO rastro de la sede en el bus era el texto
    // de Descripcion.
    private static readonly DetalleTurno DetalleConSedePrearmadaEsperado = new(
        "Turno Con Sede",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], DescripcionFranjaConSede, SedeSubaDetalle)
        }.AsReadOnly(),
        DescripcionTurnoConSede);

    // --- Issue #341: turno del catalogo con franjas MIXTAS (una con sede prearmada, otra sin) ---
    //
    // Ejercita CA-1: bajo una solicitud CON sede, cada franja resuelve su cascada de forma
    // independiente -- la que ya trae sede del catalogo la conserva, la que no la recibe de la
    // solicitud. Dos franjas en horarios distintos para no solapar (TurnoCreado.Crear valida
    // solapamiento entre ordinarias).
    private static TurnoCreado CrearEventoTurnoConFranjasMixtas() =>
        TurnoCreado.Crear(
            TurnoConFranjasMixtasId,
            "Turno Mixto",
            [
                new DatosFranja(new TimeOnly(6, 0), new TimeOnly(10, 0), [], [], SedeSuba),
                new DatosFranja(new TimeOnly(14, 0), new TimeOnly(18, 0), [], [])
            ]);

    private static readonly string DescripcionFranja1Mixta =
        $"(06:00-10:00)[{FranjaOrdinaria.Mensajes.LabelSede}:Suba]";
    private const string DescripcionFranja2Mixta = "(14:00-18:00)";
    private static readonly string DescripcionTurnoMixto =
        $"Turno Mixto {DescripcionFranja1Mixta}{DescripcionFranja2Mixta}";

    // La cascada NUNCA reconstruye el ToString() ya congelado en el catalogo (Descripcion se
    // calcula ANTES de conocer la sede de la solicitud): la franja2 queda con Sede = SedePrincipal
    // pero su Descripcion sigue siendo "(14:00-18:00)", sin el label de sede.
    private static readonly TurnoProgramado TurnoMixtoConCascadaEsperado = new(
        "Turno Mixto",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], DescripcionFranja1Mixta, SedeSuba),
            new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], DescripcionFranja2Mixta, SedePrincipal)
        }.AsReadOnly(),
        DescripcionTurnoMixto);

    private static readonly DetalleTurno DetalleMixtoConCascadaEsperado = new(
        "Turno Mixto",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], DescripcionFranja1Mixta, SedeSubaDetalle),
            new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], DescripcionFranja2Mixta, SedePrincipalDetalle)
        }.AsReadOnly(),
        DescripcionTurnoMixto);

    // --- Issue #341 CA-3: turno SIN sedes prearmadas + solicitud CON sede ---
    //
    // Es CrearEventoTurno(), la misma franja que usan los tests del camino feliz: la cascada
    // (franja.Sede ?? sedePorDefecto) aplica SedePrincipal a la UNICA franja. La Descripcion NO
    // cambia -- se congelo en el catalogo, ANTES de que la cascada conociera la sede de la solicitud.
    private static readonly TurnoProgramado TurnoProgramadoConSedeAplicadaEsperado = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)", SedePrincipal)
        }.AsReadOnly(),
        "Turno Manana (06:00-14:00)");

    private static readonly DetalleTurno DetalleConSedeAplicadaEsperado = new(
        "Turno Manana",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)", SedePrincipalDetalle)
        }.AsReadOnly(),
        "Turno Manana (06:00-14:00)");

    // --- Tests del camino feliz ---

    // CA-9, CA-10, CA-11, CA-12: emite evento de ES y publica evento publico por cada fecha
    [Fact]
    public async Task DebeEmitirProgramacionSolicitadaYPublicarEvento_CuandoDatosValidos()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Colaborador, [Fecha1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1], TurnoProgramadoEsperado));
        ThenIsPublishedPrivately(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, ColaboradorDetalle, Fecha1, DetalleEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    // Issue #319 CA-1/CA-5: el turno con descansos y extras recorre la jerarquia COMPLETA de los
    // dos payloads -- TurnoProgramado/FranjaProgramada/SubFranjaProgramada en el evento persistido y
    // DetalleTurno/DetalleFranjaOrdinaria/DetalleSubFranja en el que cruza el bus interno. Delata que
    // el mapeo del FA (unico punto de traduccion) pierda un campo anidado o permute las dos listas
    // de sub-franjas, el modo de fallo silencioso que CA-ADR-0029 decision #5 documenta.
    // Limite conocido: DatosFranja no permite declarar offsets de sub-franja, asi que por este camino
    // DiaOffsetInicio y DiaOffsetFin de las hijas siempre valen 0 y una permutacion entre ambos no
    // seria observable; los demas campos si lo son.
    [Fact]
    public async Task SolicitarProgramacionTurno_MapeaLaJerarquiaCompletaEnLosDosPayloads_CuandoElTurnoTieneDescansosYExtras()
    {
        Given(TurnoConHijasId.ToString(), CrearEventoTurnoConHijas());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoConHijasId, Colaborador, [Fecha1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1], TurnoConHijasProgramadoEsperado));
        ThenIsPublishedPrivately(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, ColaboradorDetalle, Fecha1, DetalleConHijasEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Descansos.Count, 1);
        And<SolicitudProgramacionAggregateRoot, int>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Extras.Count, 1);
    }

    // CA-11, CA-12: publica un evento publico por cada fecha (N fechas = N eventos)
    [Fact]
    public async Task DebePublicarUnEventoPorCadaFecha_CuandoHayMultiplesFechas()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Colaborador, [Fecha1, Fecha2]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1, Fecha2], TurnoProgramadoEsperado));
        ThenIsPublishedPrivately(
            new ProgramacionTurnoDiarioSolicitada(
                GuidAggregateId, ColaboradorDetalle, Fecha1, DetalleEsperado),
            new ProgramacionTurnoDiarioSolicitada(
                GuidAggregateId, ColaboradorDetalle, Fecha2, DetalleEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 2);
    }

    // Issue #331 CA-1 + Issue #341 CA-3: la sede viaja resuelta en el comando (el cliente la
    // resuelve, el servidor NUNCA consulta el maestro) y queda grabada en TRES lugares: el nivel
    // top del evento persistido (ProgramacionTurnoSolicitada.Sede -- lo SOLICITADO, trazabilidad),
    // CADA evento diario publicado al bus interno (DetalleSede, gemelo deliberado) y, desde #341,
    // la UNICA franja del turno (que no traia sede propia, asi que la cascada le aplica la de la
    // solicitud) en AMBOS payloads (persistido y bus). Dos fechas a proposito: CA-1 de #331 dice
    // "cada ProgramacionTurnoDiarioSolicitada", asi que con una sola fecha una implementacion que
    // solo poblara el primer evento pasaria en verde.
    [Fact]
    public async Task SolicitarProgramacionTurno_AplicaLaSedeDeLaSolicitudATodasLasFranjas_CuandoElTurnoNoTraeSedesPrearmadas()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Colaborador, [Fecha1, Fecha2], SedePrincipal));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1, Fecha2], TurnoProgramadoConSedeAplicadaEsperado, SedePrincipal));
        ThenIsPublishedPrivately(
            new ProgramacionTurnoDiarioSolicitada(
                GuidAggregateId, ColaboradorDetalle, Fecha1, DetalleConSedeAplicadaEsperado, SedePrincipalDetalle),
            new ProgramacionTurnoDiarioSolicitada(
                GuidAggregateId, ColaboradorDetalle, Fecha2, DetalleConSedeAplicadaEsperado, SedePrincipalDetalle));
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(s => s.Sede, SedePrincipal);
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Sede, SedePrincipal);
    }

    // Issue #341 CA-1: turno con franjas MIXTAS (una con sede prearmada del catalogo, otra sin) +
    // solicitud CON sede -> cada franja resuelve su cascada de forma INDEPENDIENTE. La franja1
    // conserva su sede propia (SedeSuba, el catalogo le gana al default); la franja2 (sin sede
    // propia) adopta la de la solicitud (SedePrincipal). Se verifica en AMBOS payloads (evento
    // persistido y evento diario publicado al bus).
    [Fact]
    public async Task SolicitarProgramacionTurno_ConservaLaSedeDeLaFranjaYAplicaLaSedePorDefecto_CuandoElTurnoTraeFranjasMixtas()
    {
        Given(TurnoConFranjasMixtasId.ToString(), CrearEventoTurnoConFranjasMixtas());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoConFranjasMixtasId, Colaborador, [Fecha1], SedePrincipal));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1], TurnoMixtoConCascadaEsperado, SedePrincipal));
        ThenIsPublishedPrivately(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, ColaboradorDetalle, Fecha1, DetalleMixtoConCascadaEsperado, SedePrincipalDetalle));
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Sede, SedeSuba);
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(
            s => s.DetalleTurno!.FranjasOrdinarias[1].Sede, SedePrincipal);
    }

    // Issue #331 CA-2: sede es opcional -- una solicitud sin sede (campo ausente) deja Sede en
    // null tanto en el evento persistido como en el evento diario publicado; el comportamiento
    // actual (anterior a este issue) queda intacto.
    [Fact]
    public async Task SolicitarProgramacionTurno_DejaSedeEnNull_CuandoLaSolicitudNoIncluyeSede()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Colaborador, [Fecha1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1], TurnoProgramadoEsperado, sede: null));
        ThenIsPublishedPrivately(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, ColaboradorDetalle, Fecha1, DetalleEsperado, sede: null));
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(s => s.Sede, null);
    }

    // Issue #335 + #341 CA-2: la sede PREARMADA en el catalogo llega intacta a AMBOS payloads
    // cuando la solicitud no trae sede propia -- la cascada es identidad y el mapeo al bus la
    // propaga. Si un refactor de ToDetalle/ObtenerDetalle dejara de copiarla, o si la cascada
    // pisara la sede del catalogo con el null de la solicitud, el defecto aparece aqui. La
    // solicitud va deliberadamente SIN sede propia para que el unico origen posible del dato sea
    // el catalogo.
    [Fact]
    public async Task SolicitarProgramacionTurno_PersisteLaSedePrearmadaDelCatalogo_CuandoElTurnoLaTraePorFranja()
    {
        Given(TurnoConSedePrearmadaId.ToString(), CrearEventoTurnoConSedePrearmada());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoConSedePrearmadaId, Colaborador, [Fecha1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1], TurnoConSedePrearmadaEsperado, sede: null));
        ThenIsPublishedPrivately(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, ColaboradorDetalle, Fecha1, DetalleConSedePrearmadaEsperado, sede: null));
        And<SolicitudProgramacionAggregateRoot, SedeProgramada?>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Sede, SedeSuba);
    }

    // CA-6: idempotencia - solicitud ya existe lanza excepcion que el endpoint mapea a 409
    [Fact]
    public async Task DebeLanzarExcepcion_CuandoSolicitudYaExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        Given(new ProgramacionTurnoSolicitada(
            GuidAggregateId, ColaboradorProgramadoEsperado, [Fecha1], TurnoProgramadoEsperado));

        var act = async () => await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Colaborador, [Fecha1]));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{SolicitarProgramacionTurnoCommandHandler.Mensajes.SolicitudYaExiste}*");
    }

    // CA-7: turno no existe en el catalogo - lanza excepcion que el endpoint mapea a 404
    [Fact]
    public async Task DebeLanzarExcepcion_CuandoTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Colaborador, [Fecha1]));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{SolicitarProgramacionTurnoCommandHandler.Mensajes.TurnoNoEncontrado}*");
    }
}
