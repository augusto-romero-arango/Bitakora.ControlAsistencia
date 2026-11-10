// Invocacion directa de los metodos estaticos de TurnoVigenteProjection, sin el DSL de
// CommandHandlerTestBase. Oraculos armados a mano (MEF-ADR-0002): los Bloques esperados se calculan
// aplicando a mano el algoritmo de TurnoDiario.Segmentar (cubierto por sus propios tests); aqui se
// verifica que Create/Apply lo invocan y mapean cada BloqueTurno al Bloque de la vista.
//
// Alias TipoBloqueVigente: DomainEvents.TipoBloque y ReadModels.ControlHoras.TipoBloque comparten
// nombre a proposito (tres islas); con ambos using el nombre corto es ambiguo (CS0104).

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

    // NombreCompleto llega ya concatenado en la terna de identidad: el worker no lo compone.
    [Fact]
    public void Create_ProyectaElTurnoVigenteCompleto_DesdeTurnoDiarioAsignado()
    {
        var colaborador = ColaboradorDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "cd:EMP-001:20260803";

        // El descanso y el extra previo cubren los tres TipoBloque sin cruzar medianoche, para
        // calcular el oraculo a mano sin ambiguedad.
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

    // Id, CodigoColaborador y Fecha se verifican junto al refresco para que el test no pase
    // sobrescribiendo la vista entera.
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

    // Franja sin Sede: turno prearmado multi-sede sin resolver o evento anterior a la sede por franja.
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
