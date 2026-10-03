using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using TipoBloqueVigente = Bitakora.ControlAsistencia.ReadModels.ControlHoras.TipoBloque;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

// Issue #756: la programacion vigente refleja ausencias y cancelaciones de turno.
// Oraculos armados a mano (MEF-ADR-0002).
public class TurnoVigenteAusenciasProjectionTests
{
    private const string StreamKey = "cd:EMP-001:20260803";
    private static readonly DateOnly Fecha = new(2026, 8, 3);
    private static readonly ColaboradorProgramado Colaborador = new("CC-1098765432", "EMP-001", "Ana Ramirez");
    private static readonly Guid AusenciaId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static IReadOnlyList<Bloque> BloquesManana() =>
        [new Bloque(TipoBloqueVigente.Ordinaria, Fecha.ToDateTime(new TimeOnly(6, 0)), Fecha.ToDateTime(new TimeOnly(14, 0)))];

    private static TurnoVigente VistaConTurno() =>
        new(StreamKey, "EMP-001", "Ana Ramirez", Fecha, "Turno Manana", "Turno Manana: (06:00-14:00)", BloquesManana());

    private static TurnoVigente VistaConAusenciaSobreTurno() =>
        new(StreamKey, "EMP-001", "Ana Ramirez", Fecha, "", "", [],
            "Vacaciones", AusenciaId,
            new TurnoCubierto("Turno Manana", "Turno Manana: (06:00-14:00)", BloquesManana()));

    private static TurnoVigente VistaConAusenciaSola() =>
        new(StreamKey, "EMP-001", "Ana Ramirez", Fecha, "", "", [], "Vacaciones", AusenciaId);

    private static AusenciaDiariaAsignada AusenciaAsignada() =>
        AusenciaDiariaAsignada.Crear(StreamKey, Colaborador, Fecha, AusenciaId, "Vacaciones");

    private static CancelacionAusenciaDiariaRegistrada CancelacionAusencia() =>
        CancelacionAusenciaDiariaRegistrada.Crear(StreamKey, AusenciaId, Fecha);

    private static TurnoDiarioCancelado TurnoCancelado() =>
        TurnoDiarioCancelado.Crear(StreamKey, Colaborador, Fecha, Guid.NewGuid());

    private static TurnoDiarioAsignado TurnoTarde()
    {
        var franja = new FranjaProgramada(
            new TimeOnly(14, 0), new TimeOnly(22, 0), DiaOffsetFin: 0,
            Descansos: [], Extras: [], Descripcion: "(14:00-22:00)");
        return new TurnoDiarioAsignado(
            StreamKey, Colaborador, Fecha,
            new TurnoDiario("Turno Tarde", [franja], "Turno Tarde: (14:00-22:00)"), Guid.NewGuid());
    }

    // CA-3
    [Fact]
    public void Create_ProyectaAusenciaSinBloques_DesdeAusenciaDiariaAsignada()
    {
        var vista = TurnoVigenteProjection.Create(AusenciaAsignada());

        vista.Id.Should().Be(StreamKey);
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.NombreCompleto.Should().Be("Ana Ramirez");
        vista.Fecha.Should().Be(Fecha);
        vista.MotivoAusencia.Should().Be("Vacaciones");
        vista.AusenciaId.Should().Be(AusenciaId);
        vista.Bloques.Should().BeEmpty();
        vista.TurnoCubierto.Should().BeNull();
    }

    // CA-2
    [Fact]
    public void Apply_MuestraAusenciaSinBloquesYConservaTurnoCubierto_CuandoAusenciaAsignadaSobreTurno()
    {
        var vista = TurnoVigenteProjection.Apply(AusenciaAsignada(), VistaConTurno());

        vista.MotivoAusencia.Should().Be("Vacaciones");
        vista.AusenciaId.Should().Be(AusenciaId);
        vista.Bloques.Should().BeEmpty();
        vista.TurnoCubierto.Should().Be(
            new TurnoCubierto("Turno Manana", "Turno Manana: (06:00-14:00)", BloquesManana()));
    }

    // CA-4
    [Fact]
    public void Apply_RestableceElTurno_CuandoCancelacionDeAusenciaConTurnoDebajo()
    {
        var vista = TurnoVigenteProjection.Apply(CancelacionAusencia(), VistaConAusenciaSobreTurno())!;

        vista.NombreTurno.Should().Be("Turno Manana");
        vista.HorarioResumido.Should().Be("Turno Manana: (06:00-14:00)");
        vista.Bloques.Should().BeEquivalentTo(BloquesManana());
        vista.MotivoAusencia.Should().BeNull();
        vista.AusenciaId.Should().BeNull();
        vista.TurnoCubierto.Should().BeNull();
    }

    // CA-4
    [Fact]
    public void Apply_NoBorra_CuandoCancelacionDeAusenciaConTurnoDebajo()
    {
        TurnoVigenteProjection.Apply(CancelacionAusencia(), VistaConAusenciaSobreTurno())
            .Should().NotBeNull();
    }

    // CA-4
    [Fact]
    public void Apply_Borra_CuandoCancelacionDeAusenciaSinTurnoDebajo()
    {
        TurnoVigenteProjection.Apply(CancelacionAusencia(), VistaConAusenciaSola())
            .Should().BeNull();
    }

    // CA-1
    [Fact]
    public void Apply_Borra_CuandoTurnoDiarioCanceladoSinAusencia()
    {
        TurnoVigenteProjection.Apply(TurnoCancelado(), VistaConTurno()).Should().BeNull();
    }

    // CA-5
    [Fact]
    public void Apply_NoBorra_CuandoTurnoDiarioCanceladoConAusencia()
    {
        TurnoVigenteProjection.Apply(TurnoCancelado(), VistaConAusenciaSobreTurno())
            .Should().NotBeNull();
    }

    // CA-5
    [Fact]
    public void Apply_SigueMostrandoAusenciaYDescartaTurnoCubierto_CuandoTurnoDiarioCanceladoConAusencia()
    {
        var vista = TurnoVigenteProjection.Apply(TurnoCancelado(), VistaConAusenciaSobreTurno())!;

        vista.MotivoAusencia.Should().Be("Vacaciones");
        vista.Bloques.Should().BeEmpty();
        vista.TurnoCubierto.Should().BeNull();
    }

    // CA-5
    [Fact]
    public void Apply_SigueMostrandoAusenciaYActualizaTurnoCubierto_CuandoTurnoAsignadoConAusencia()
    {
        var medianoche = Fecha.ToDateTime(TimeOnly.MinValue);

        var vista = TurnoVigenteProjection.Apply(TurnoTarde(), VistaConAusenciaSobreTurno());

        vista.MotivoAusencia.Should().Be("Vacaciones");
        vista.Bloques.Should().BeEmpty();
        vista.TurnoCubierto.Should().Be(new TurnoCubierto(
            "Turno Tarde", "Turno Tarde: (14:00-22:00)",
            [new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(14), medianoche.AddHours(22))]));
    }
}
