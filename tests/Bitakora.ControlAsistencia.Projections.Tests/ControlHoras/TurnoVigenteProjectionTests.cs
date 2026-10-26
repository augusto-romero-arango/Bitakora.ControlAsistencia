// Issue #328: fase roja de la segunda proyeccion concreta del BC. Invocacion DIRECTA de los
// metodos estaticos de TurnoVigenteProjection (N1, MEF-ADR-0035) -- no el DSL Given/When/Then de
// CommandHandlerTestBase (MEF-ADR-0002, testea command handlers contra el event store): aqui se
// testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se
// reusa la logica de Create/Apply bajo prueba para construir el valor esperado. Los Bloques
// esperados SI se calculan aplicando a mano el algoritmo documentado de TurnoDiario.Segmentar
// (issue #327, ya cubierto por sus propios tests) -- Segmentar no es la logica bajo prueba en este
// archivo; lo que se verifica aqui es que Create/Apply lo invocan sobre el payload del evento
// (Tell-don't-Ask, MEF-ADR-0012) y mapean cada BloqueTurno resultante al record Bloque propio de
// la vista (ReadModels, sin relacion de tipo con DomainEvents).
//
// using TipoBloqueVigente: alias obligatorio -- DomainEvents.TipoBloque (necesario para construir
// el evento fundacional) y ReadModels.ControlHoras.TipoBloque (necesario para el oraculo de la
// vista) comparten nombre a proposito, mismo criterio de "tres islas" que ya aplican Colaborador/
// TurnoDiario/FranjaProgramada entre los ensamblados de eventos. Con ambos "using" activos el
// simbolo corto "TipoBloque" queda ambiguo (CS0104); el alias resuelve solo el lado ReadModels.
//
// Issue #337 (fase roja): extension de este archivo -- CA-1, mapeo de SedeId/NombreSede en cada
// Bloque desde la SedeProgramada que #336 ya estampa en cada BloqueTurno. MapearBloque (produccion
// actual) ignora bloque.Sede por completo, asi que estos tests quedan en rojo hasta que
// projection-implementer lo propague; ninguno reusa el algoritmo de Segmentar como oraculo, se
// arma a mano igual que los tests de arriba (MEF-ADR-0002).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using TipoBloqueVigente = Bitakora.ControlAsistencia.ReadModels.ControlHoras.TipoBloque;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

public class TurnoVigenteProjectionTests
{
    private static ColaboradorProgramado ColaboradorDePrueba() =>
        new("CC-1098765432", "EMP-001", "Ana Ramirez");

    // CA-1: Create mapea stream key, CodigoColaborador, NombreCompleto (llega ya concatenado en la
    // terna de identidad -- el worker no lo compone), NombreTurno,
    // HorarioResumido (la Descripcion textual que el evento ya trae) y los Bloques que produce
    // Segmentar, con los tres tipos posibles (Ordinaria/Descanso/Extra) representados.
    [Fact]
    public void Create_ProyectaElTurnoVigenteCompleto_DesdeTurnoDiarioAsignado()
    {
        var colaborador = ColaboradorDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "cd:EMP-001:20260803";

        // Franja 06:00-14:00 con un descanso (10:00-10:15) y un extra ANTES del inicio nominal
        // (05:00-06:00): cubre los tres TipoBloque sin que ningun tramo cruce medianoche, para
        // poder calcular el oraculo a mano sin ambiguedad (Tramo.RomperEnMedianoche no interviene).
        var franja = new FranjaProgramada(
            new TimeOnly(6, 0),
            new TimeOnly(14, 0),
            DiaOffsetFin: 0,
            Descansos: [new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)")],
            Extras: [new SubFranjaProgramada(new TimeOnly(5, 0), new TimeOnly(6, 0), 0, 0, "(05:00-06:00)")],
            Descripcion: "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(05:00-06:00)]");
        var turnoDiario = new TurnoDiario(
            "Turno Manana",
            [franja],
            "Turno Manana: (06:00-14:00)[Descansos:(10:00-10:15)][Extras:(05:00-06:00)]");
        var solicitudId = Guid.NewGuid();
        var evento = new TurnoDiarioAsignado(streamKey, colaborador, fecha, turnoDiario, solicitudId);

        var vista = TurnoVigenteProjection.Create(evento);

        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);
        var bloquesEsperados = new[]
        {
            new Bloque(TipoBloqueVigente.Extra, medianoche.AddHours(5), medianoche.AddHours(6)),
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(10)),
            new Bloque(TipoBloqueVigente.Descanso, medianoche.AddHours(10), medianoche.AddHours(10).AddMinutes(15)),
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(10).AddMinutes(15), medianoche.AddHours(14)),
        };

        vista.Id.Should().Be(streamKey);
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.NombreCompleto.Should().Be("Ana Ramirez");
        vista.Fecha.Should().Be(fecha);
        vista.NombreTurno.Should().Be("Turno Manana");
        vista.HorarioResumido.Should().Be(
            "Turno Manana: (06:00-14:00)[Descansos:(10:00-10:15)][Extras:(05:00-06:00)]");
        vista.Bloques.Should().Equal(bloquesEsperados);
    }

    // CA-2: la reasignacion sobrescribe -- dos TurnoDiarioAsignado consecutivos sobre el mismo
    // (colaborador, fecha) dejan la vista con el NombreTurno, HorarioResumido y Bloques del SEGUNDO
    // evento ("el ultimo gana"). Id, CodigoColaborador y Fecha no cambian (mismo stream key). Sin
    // ShouldDelete (el turno vigente nunca se borra, solo se reasigna) -- no hay metodo que probar.
    [Fact]
    public void Apply_SobrescribeTurnoHorarioYBloques_CuandoLlegaOtroTurnoDiarioAsignado()
    {
        var colaborador = ColaboradorDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "cd:EMP-001:20260803";
        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);

        var vistaPrevia = new TurnoVigente(
            streamKey,
            "EMP-001",
            "Ana Ramirez",
            fecha,
            "Turno Manana",
            "Turno Manana: (06:00-14:00)",
            [new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(14))]);

        // Segundo turno, sin descansos ni extras: un solo bloque Ordinaria 14:00-22:00.
        var franjaTarde = new FranjaProgramada(
            new TimeOnly(14, 0), new TimeOnly(22, 0), DiaOffsetFin: 0,
            Descansos: [], Extras: [], Descripcion: "(14:00-22:00)");
        var turnoTarde = new TurnoDiario("Turno Tarde", [franjaTarde], "Turno Tarde: (14:00-22:00)");
        var nuevaSolicitudId = Guid.NewGuid();
        var segundoEvento = new TurnoDiarioAsignado(streamKey, colaborador, fecha, turnoTarde, nuevaSolicitudId);

        var vista = TurnoVigenteProjection.Apply(segundoEvento, vistaPrevia);

        vista.Id.Should().Be(streamKey);
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.Fecha.Should().Be(fecha);
        vista.NombreCompleto.Should().Be("Ana Ramirez");
        vista.NombreTurno.Should().Be("Turno Tarde");
        vista.HorarioResumido.Should().Be("Turno Tarde: (14:00-22:00)");
        vista.Bloques.Should().Equal(
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(14), medianoche.AddHours(22)));
    }

    // Borde que el test de arriba no discrimina, porque ahi los dos eventos traen el MISMO
    // colaborador: cada TurnoDiarioAsignado carga la terna de identidad, asi que una correccion
    // del nombre aguas arriba llega con la reasignacion y el "ultimo gana" tambien le aplica --
    // congelar el nombre de la primera asignacion dejaria la vista mostrando un dato viejo para
    // siempre. Id, CodigoColaborador y Fecha si son invariantes (identidad del stream), y se verifican aqui
    // junto al refresco para que el test no pueda pasar por sobrescribir la vista entera.
    [Fact]
    public void Apply_RefrescaElNombreCompleto_CuandoLaReasignacionTraeElNombreCorregido()
    {
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "cd:EMP-001:20260803";
        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);

        var vistaPrevia = new TurnoVigente(
            streamKey,
            "EMP-001",
            "Ana Ramirez",
            fecha,
            "Turno Manana",
            "Turno Manana: (06:00-14:00)",
            [new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(14))]);

        // Mismo CodigoColaborador, nombre corregido aguas arriba (dos nombres y dos apellidos).
        var colaboradorCorregido = new ColaboradorProgramado("CC-1098765432", "EMP-001", "Ana Maria Ramirez Solano");
        var turnoTarde = new TurnoDiario(
            "Turno Tarde",
            [new FranjaProgramada(
                new TimeOnly(14, 0), new TimeOnly(22, 0), DiaOffsetFin: 0,
                Descansos: [], Extras: [], Descripcion: "(14:00-22:00)")],
            "Turno Tarde: (14:00-22:00)");
        var segundoEvento = new TurnoDiarioAsignado(
            streamKey, colaboradorCorregido, fecha, turnoTarde, Guid.NewGuid());

        var vista = TurnoVigenteProjection.Apply(segundoEvento, vistaPrevia);

        vista.NombreCompleto.Should().Be("Ana Maria Ramirez Solano");
        vista.Id.Should().Be(streamKey);
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.Fecha.Should().Be(fecha);
    }

    // --- Issue #337: mapeo de sede por bloque (CA-1) ---

    // CA-1: una franja sin descansos ni extras con Sede asignada produce un unico Bloque Ordinaria
    // con SedeId/NombreSede tomados de esa SedeProgramada. Hoy MapearBloque ignora bloque.Sede por
    // completo (produccion actual: "new(MapearTipo(bloque.Tipo), bloque.Inicio, bloque.Fin)"), asi
    // que este test queda en rojo hasta que projection-implementer propague el dato.
    [Fact]
    public void Create_MapeaSedeIdYNombreSede_DesdeLaSedeDeLaFranja()
    {
        var colaborador = ColaboradorDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "cd:EMP-001:20260803";
        var sede = new SedeProgramada("SD-SUBA", "Suba");

        var franja = new FranjaProgramada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), DiaOffsetFin: 0,
            Descansos: [], Extras: [], Descripcion: "(06:00-14:00)", Sede: sede);
        var turnoDiario = new TurnoDiario("Turno Manana", [franja], "Turno Manana: (06:00-14:00)");
        var evento = new TurnoDiarioAsignado(streamKey, colaborador, fecha, turnoDiario, Guid.NewGuid());

        var vista = TurnoVigenteProjection.Create(evento);

        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);
        vista.Bloques.Should().Equal(
            new Bloque(
                TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(14),
                SedeId: "SD-SUBA", NombreSede: "Suba"));
    }

    // CA-1 (issue #336, "Los bloques de descanso y extra heredan la sede de la franja madre que
    // los contiene"): los CUATRO bloques que produce Segmentar sobre una franja con descanso y
    // extra (mismo turno del test CA-1 de #328, arriba) heredan la MISMA sede de la franja madre --
    // ninguno tiene sede propia.
    [Fact]
    public void Create_HeredaLaSedeDeLaFranjaEnBloquesDeDescansoYExtra()
    {
        var colaborador = ColaboradorDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "cd:EMP-001:20260803";
        var sede = new SedeProgramada("SD-SUBA", "Suba");

        var franja = new FranjaProgramada(
            new TimeOnly(6, 0),
            new TimeOnly(14, 0),
            DiaOffsetFin: 0,
            Descansos: [new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)")],
            Extras: [new SubFranjaProgramada(new TimeOnly(5, 0), new TimeOnly(6, 0), 0, 0, "(05:00-06:00)")],
            Descripcion: "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(05:00-06:00)]",
            Sede: sede);
        var turnoDiario = new TurnoDiario(
            "Turno Manana",
            [franja],
            "Turno Manana: (06:00-14:00)[Descansos:(10:00-10:15)][Extras:(05:00-06:00)]");
        var evento = new TurnoDiarioAsignado(streamKey, colaborador, fecha, turnoDiario, Guid.NewGuid());

        var vista = TurnoVigenteProjection.Create(evento);

        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);
        var bloquesEsperados = new[]
        {
            new Bloque(TipoBloqueVigente.Extra, medianoche.AddHours(5), medianoche.AddHours(6), "SD-SUBA", "Suba"),
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(10), "SD-SUBA", "Suba"),
            new Bloque(TipoBloqueVigente.Descanso, medianoche.AddHours(10), medianoche.AddHours(10).AddMinutes(15), "SD-SUBA", "Suba"),
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(10).AddMinutes(15), medianoche.AddHours(14), "SD-SUBA", "Suba"),
        };
        vista.Bloques.Should().Equal(bloquesEsperados);
    }

    // CA-1, segunda mitad: "los bloques de franjas sin sede quedan con ambos campos null" -- una
    // franja sin Sede (default, turno prearmado multi-sede sin resolver o evento anterior a #336)
    // no inventa sede en ninguno de sus bloques. Oraculo explicito para que una implementacion
    // futura no pueda "adivinar" un valor no-null y seguir pasando por casualidad.
    [Fact]
    public void Create_DejaSedeIdYNombreSedeNulos_CuandoLaFranjaNoTraeSede()
    {
        var colaborador = ColaboradorDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "cd:EMP-001:20260803";

        var franja = new FranjaProgramada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), DiaOffsetFin: 0,
            Descansos: [], Extras: [], Descripcion: "(06:00-14:00)");
        var turnoDiario = new TurnoDiario("Turno Manana", [franja], "Turno Manana: (06:00-14:00)");
        var evento = new TurnoDiarioAsignado(streamKey, colaborador, fecha, turnoDiario, Guid.NewGuid());

        var vista = TurnoVigenteProjection.Create(evento);

        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);
        vista.Bloques.Should().Equal(
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(14)));
    }

    // CA-2 (semantica del filtro fijada por el escenario de la conversacion del issue): un turno
    // partido con dos franjas contiguas en sedes DISTINTAS (Suba en la manana, Chapinero en la
    // tarde) produce bloques con la sede de SU PROPIA franja, no la del turno completo -- la razon
    // de ser de "sede por bloque, nunca por dia" (issue #337, "Contexto").
    [Fact]
    public void Create_MapeaSedesDistintasPorBloque_CuandoElTurnoTieneFranjasEnSedesDistintas()
    {
        var colaborador = ColaboradorDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "cd:EMP-001:20260803";
        var suba = new SedeProgramada("SD-SUBA", "Suba");
        var chapinero = new SedeProgramada("SD-CHAPINERO", "Chapinero");

        var franjaManana = new FranjaProgramada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), DiaOffsetFin: 0,
            Descansos: [], Extras: [], Descripcion: "(06:00-14:00)", Sede: suba);
        var franjaTarde = new FranjaProgramada(
            new TimeOnly(14, 0), new TimeOnly(22, 0), DiaOffsetFin: 0,
            Descansos: [], Extras: [], Descripcion: "(14:00-22:00)", Sede: chapinero);
        var turnoDiario = new TurnoDiario(
            "Turno Partido", [franjaManana, franjaTarde], "Turno Partido: (06:00-14:00)/(14:00-22:00)");
        var evento = new TurnoDiarioAsignado(streamKey, colaborador, fecha, turnoDiario, Guid.NewGuid());

        var vista = TurnoVigenteProjection.Create(evento);

        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);
        var bloquesEsperados = new[]
        {
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(14), "SD-SUBA", "Suba"),
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(14), medianoche.AddHours(22), "SD-CHAPINERO", "Chapinero"),
        };
        vista.Bloques.Should().Equal(bloquesEsperados);
    }

    // CA-2/CA-1: "el ultimo gana" (semantica ya fijada por #328) aplica igual a la sede -- una
    // reasignacion que cambia de sede no deja bloques con la sede VIEJA mezclada con datos nuevos.
    // vistaPrevia simula un documento materializado antes de esta reasignacion, ya con sede (no el
    // caso null de CA-5 -- ese es un dato persistido sin este issue desplegado, no ejercitable
    // desde Apply).
    [Fact]
    public void Apply_SobrescribeLaSedeDeLosBloques_CuandoLaReasignacionCambiaDeSede()
    {
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "cd:EMP-001:20260803";
        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);

        var vistaPrevia = new TurnoVigente(
            streamKey,
            "EMP-001",
            "Ana Ramirez",
            fecha,
            "Turno Manana",
            "Turno Manana: (06:00-14:00)",
            [new Bloque(
                TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(14),
                "SD-SUBA", "Suba")]);

        var chapinero = new SedeProgramada("SD-CHAPINERO", "Chapinero");
        var franjaTarde = new FranjaProgramada(
            new TimeOnly(14, 0), new TimeOnly(22, 0), DiaOffsetFin: 0,
            Descansos: [], Extras: [], Descripcion: "(14:00-22:00)", Sede: chapinero);
        var turnoTarde = new TurnoDiario("Turno Tarde", [franjaTarde], "Turno Tarde: (14:00-22:00)");
        var segundoEvento = new TurnoDiarioAsignado(
            streamKey, ColaboradorDePrueba(), fecha, turnoTarde, Guid.NewGuid());

        var vista = TurnoVigenteProjection.Apply(segundoEvento, vistaPrevia);

        vista.Bloques.Should().Equal(
            new Bloque(
                TipoBloqueVigente.Ordinaria, medianoche.AddHours(14), medianoche.AddHours(22),
                "SD-CHAPINERO", "Chapinero"));
    }
}

// Issue #1236: la marca de horario abierto llega en DetalleTurno.EsHorarioAbierto (#1235).
public class TurnoVigenteHorarioAbiertoProjectionTests
{
    private const string StreamKey = "cd:EMP-001:20260803";
    private static readonly DateOnly Fecha = new(2026, 8, 3);
    private static readonly ColaboradorProgramado Colaborador = new("CC-1098765432", "EMP-001", "Ana Ramirez");

    private static TurnoDiarioAsignado Asignado(bool abierto)
    {
        var franja = new FranjaProgramada(
            new TimeOnly(4, 0), new TimeOnly(6, 0), DiaOffsetFin: 0,
            Descansos: [], Extras: [], Descripcion: "(04:00-06:00)");
        return new TurnoDiarioAsignado(
            StreamKey, Colaborador, Fecha,
            new TurnoDiario("Turno Abierto", [franja], "Turno Abierto: (04:00-06:00)", EsHorarioAbierto: abierto),
            Guid.NewGuid());
    }

    private static TurnoVigente Vista(bool abierto) =>
        new(StreamKey, "EMP-001", "Ana Ramirez", Fecha, "Turno Abierto", "Turno Abierto: (04:00-06:00)",
            [new Bloque(TipoBloqueVigente.Ordinaria, Fecha.ToDateTime(new TimeOnly(4, 0)), Fecha.ToDateTime(new TimeOnly(6, 0)))],
            EsHorarioAbierto: abierto);

    [Fact]
    public void Create_MarcaHorarioAbierto_CuandoTurnoDiarioAsignadoEsDeHorarioAbierto() =>
        TurnoVigenteProjection.Create(Asignado(true)).EsHorarioAbierto.Should().BeTrue();

    [Fact]
    public void Create_NoMarcaHorarioAbierto_CuandoTurnoDiarioAsignadoEsComun() =>
        TurnoVigenteProjection.Create(Asignado(false)).EsHorarioAbierto.Should().BeFalse();

    [Fact]
    public void Apply_ActivaLaMarca_CuandoReasignacionEsDeHorarioAbierto() =>
        TurnoVigenteProjection.Apply(Asignado(true), Vista(false)).EsHorarioAbierto.Should().BeTrue();

    [Fact]
    public void Apply_ApagaLaMarca_CuandoReasignacionEsComun() =>
        TurnoVigenteProjection.Apply(Asignado(false), Vista(true)).EsHorarioAbierto.Should().BeFalse();

    [Fact]
    public void Apply_MueveLaMarcaATurnoCubierto_CuandoAusenciaAsignadaSobreHorarioAbierto()
    {
        var ausencia = AusenciaDiariaAsignada.Crear(StreamKey, Colaborador, Fecha, Guid.NewGuid(), "Vacaciones");

        var vista = TurnoVigenteProjection.Apply(ausencia, Vista(true));

        vista.EsHorarioAbierto.Should().BeFalse();
        vista.TurnoCubierto!.EsHorarioAbierto.Should().BeTrue();
    }

    [Fact]
    public void Apply_RestableceLaMarca_CuandoCancelacionDeAusenciaSobreHorarioAbierto()
    {
        var ausenciaId = Guid.NewGuid();
        var bloques = Vista(true).Bloques;
        var conAusencia = new TurnoVigente(StreamKey, "EMP-001", "Ana Ramirez", Fecha, "", "", [],
            "Vacaciones", ausenciaId, new TurnoCubierto("Turno Abierto", "Turno Abierto: (04:00-06:00)", bloques, true));

        var vista = TurnoVigenteProjection.Apply(
            CancelacionAusenciaDiariaRegistrada.Crear(StreamKey, ausenciaId, Fecha), conAusencia)!;

        vista.EsHorarioAbierto.Should().BeTrue();
        vista.TurnoCubierto.Should().BeNull();
    }

    [Fact]
    public void Apply_ActualizaSoloTurnoCubierto_CuandoTurnoDiarioAsignadoConAusenciaVigente()
    {
        var conAusencia = new TurnoVigente(StreamKey, "EMP-001", "Ana Ramirez", Fecha, "", "", [],
            "Vacaciones", Guid.NewGuid(), new TurnoCubierto("Otro", "Otro", [], false));

        var vista = TurnoVigenteProjection.Apply(Asignado(true), conAusencia);

        vista.EsHorarioAbierto.Should().BeFalse();
        vista.TurnoCubierto!.EsHorarioAbierto.Should().BeTrue();
    }

    [Fact]
    public void Deserializar_AsumeFalse_CuandoJsonLegadoNoTraeLaClave()
    {
        const string json = """
            {"Id":"cd:EMP-001:20260803","CodigoColaborador":"EMP-001","NombreCompleto":"Ana","Fecha":"2026-08-03",
             "NombreTurno":"T","HorarioResumido":"H","Bloques":[],
             "TurnoCubierto":{"NombreTurno":"T","HorarioResumido":"H","Bloques":[]}}
            """;

        var vista = System.Text.Json.JsonSerializer.Deserialize<TurnoVigente>(json)!;

        vista.EsHorarioAbierto.Should().BeFalse();
        vista.TurnoCubierto!.EsHorarioAbierto.Should().BeFalse();
    }
}
