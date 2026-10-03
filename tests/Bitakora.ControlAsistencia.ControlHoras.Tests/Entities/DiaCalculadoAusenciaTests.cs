using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using DepuracionDiaRecibida = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.DepuracionDiaRecibida;
using DiaAprobado = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.DiaAprobado;
using EventoFranjaDepurada = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.FranjaDepurada;
using EventoMarcacionDelDia = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.MarcacionDelDia;
using HorasDiscriminadas = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.HorasDiscriminadas;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class DiaCalculadoAusenciaTests
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 10, 5);
    private static readonly string StreamId = DiaCalculadoAggregateRoot.ComputarStreamId(CodigoColaborador, Fecha);
    private static readonly DateTime Entrada = new(2026, 10, 5, 6, 0, 0);
    private static readonly DateTime Salida = new(2026, 10, 5, 14, 0, 0);

    private static HorasDiscriminadas SinHoras() => new(new Dictionary<string, decimal>(), []);

    private static DepuracionDiaRecibida Foto(
        string? nombreTurno,
        IReadOnlyList<EventoFranjaDepurada> franjas,
        IReadOnlyList<EventoMarcacionDelDia> marcaciones,
        string? motivo) =>
        new(StreamId, CodigoColaborador, Fecha, null, nombreTurno, franjas, marcaciones, SinHoras(), motivo);

    private static DiaCalculadoAggregateRoot Hidratar(DepuracionDiaRecibida foto)
    {
        var dia = new DiaCalculadoAggregateRoot();
        dia.Apply(foto);
        return dia;
    }

    // CA-1
    [Fact]
    public void GenerarDepuracionDelDia_ClasificaAusenciaConMotivoYMarcacionesNoUsadas_CuandoLaFotoTraeMotivo()
    {
        var dia = Hidratar(Foto(null, [], [new EventoMarcacionDelDia(Entrada, "Entrada")], "Vacaciones"));

        var vista = dia.GenerarDepuracionDelDia();

        vista.Plan.Should().Be(PlanDelDia.Ausencia);
        vista.MotivoAusencia.Should().Be("Vacaciones");
        vista.Franjas.Should().BeEmpty();
        vista.Marcaciones.Should().ContainSingle().Which.Usada.Should().BeFalse();
    }

    // CA-1: el motivo tiene precedencia sobre la senal estructural (nombre + cero franjas = Descanso).
    [Fact]
    public void GenerarDepuracionDelDia_ClasificaAusencia_CuandoHayMotivoYElTurnoEraDescanso()
    {
        var dia = Hidratar(Foto("Descanso", [], [], "IncapacidadMedica"));

        var vista = dia.GenerarDepuracionDelDia();

        vista.Plan.Should().Be(PlanDelDia.Ausencia);
        vista.MotivoAusencia.Should().Be("IncapacidadMedica");
    }

    // CA-2
    [Fact]
    public void GenerarDepuracionDelDia_ConservaElPlanDelTurnoYMotivoNulo_CuandoLaFotoNoTraeMotivo()
    {
        var franja = new EventoFranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false);
        var dia = Hidratar(Foto("Manana", [franja], [], null));

        var vista = dia.GenerarDepuracionDelDia();

        vista.Plan.Should().Be(PlanDelDia.ConJornada);
        vista.MotivoAusencia.Should().BeNull();
    }

    // CA-4
    [Fact]
    public void GenerarDepuracionDelDia_VuelveAlPlanDelTurno_CuandoUnaFotoSinMotivoSigueAlDiaDeAusencia()
    {
        var dia = Hidratar(Foto(null, [], [], "Vacaciones"));
        var franja = new EventoFranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false);

        dia.Apply(Foto("Manana", [franja], [], null));
        var vista = dia.GenerarDepuracionDelDia();

        vista.Plan.Should().Be(PlanDelDia.ConJornada);
        vista.MotivoAusencia.Should().BeNull();
    }

    // CA-5
    [Fact]
    public void GenerarDepuracionDelDia_ConservaAusenciaYMotivo_CuandoElDiaDeAusenciaEstaAprobado()
    {
        var dia = Hidratar(Foto(null, [], [new EventoMarcacionDelDia(Entrada, "Entrada")], "LicenciaRemunerada"));

        dia.Apply(DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));
        var vista = dia.GenerarDepuracionDelDia();

        vista.Estado.Should().Be(EstadoAsistencia.Aprobado);
        vista.Plan.Should().Be(PlanDelDia.Ausencia);
        vista.MotivoAusencia.Should().Be("LicenciaRemunerada");
    }
}
