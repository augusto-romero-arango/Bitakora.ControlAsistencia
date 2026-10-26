using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using TipoBloqueVigente = Bitakora.ControlAsistencia.ReadModels.ControlHoras.TipoBloque;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

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

    [Fact]
    public void TurnoCubierto_NoEsIgual_CuandoSoloDifiereLaMarcaDeHorarioAbierto()
    {
        var bloques = Vista(true).Bloques;

        new TurnoCubierto("Turno Abierto", "H", bloques, EsHorarioAbierto: true)
            .Should().NotBe(new TurnoCubierto("Turno Abierto", "H", bloques, EsHorarioAbierto: false));
    }
}
