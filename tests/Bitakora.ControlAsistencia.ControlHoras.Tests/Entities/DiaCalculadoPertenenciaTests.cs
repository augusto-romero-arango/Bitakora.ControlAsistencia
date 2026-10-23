using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using DepuracionDiaRecibida = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.DepuracionDiaRecibida;
using EventoFranjaDepurada = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.FranjaDepurada;
using EventoMarcacionDelDia = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.MarcacionDelDia;
using HorasDiscriminadas = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.HorasDiscriminadas;
using VistaPertenencia = Bitakora.ControlAsistencia.ReadModels.ControlHoras.PertenenciaMarcacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class DiaCalculadoPertenenciaTests
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 10, 5);
    private static readonly string StreamId = DiaCalculadoAggregateRoot.ComputarStreamId(CodigoColaborador, Fecha);
    private static readonly DateTime Entrada = new(2026, 10, 5, 6, 0, 0);
    private static readonly DateTime Salida = new(2026, 10, 5, 14, 0, 0);
    private static readonly DateTime Madrugada = new(2026, 10, 5, 4, 30, 0);
    private static readonly DateTime Noche = new(2026, 10, 5, 21, 0, 0);

    private static readonly EventoFranjaDepurada Franja =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false);

    private static DiaCalculadoAggregateRoot Hidratar(
        string? nombreTurno,
        IReadOnlyList<EventoFranjaDepurada> franjas,
        IReadOnlyList<EventoMarcacionDelDia> marcaciones,
        string? motivo = null)
    {
        var dia = new DiaCalculadoAggregateRoot();
        dia.Apply(new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, nombreTurno, franjas, marcaciones,
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), motivo));
        return dia;
    }

    [Fact]
    public void GenerarDepuracionDelDia_ExponeLaPertenenciaDeCadaMarcacion_CuandoLaFotoLaTrae()
    {
        var dia = Hidratar("Manana", [Franja],
        [
            new EventoMarcacionDelDia(Madrugada, "Entrada", Pertenencia: "Ambigua"),
            new EventoMarcacionDelDia(Entrada, "Entrada", Pertenencia: "DelTurno"),
            new EventoMarcacionDelDia(Noche, "Salida", Pertenencia: "FueraDeCobertura")
        ]);

        var vista = dia.GenerarDepuracionDelDia();

        vista.Marcaciones.Select(m => m.Pertenencia).Should().Equal(
            VistaPertenencia.Ambigua, VistaPertenencia.DelTurno, VistaPertenencia.FueraDeCobertura);
        vista.Marcaciones.Select(m => m.Usada).Should().Equal(false, true, false);
    }

    [Fact]
    public void GenerarDepuracionDelDia_LeeDelTurnoEnCadaMarcacion_CuandoLaFotoLegadaNoTraeLaClaveYHayJornada()
    {
        var dia = Hidratar("Manana", [Franja],
        [
            new EventoMarcacionDelDia(Entrada, "Entrada"),
            new EventoMarcacionDelDia(Noche, "Salida")
        ]);

        var vista = dia.GenerarDepuracionDelDia();

        vista.Marcaciones.Select(m => m.Pertenencia).Should().Equal(
            VistaPertenencia.DelTurno, VistaPertenencia.DelTurno);
    }

    [Fact]
    public void GenerarDepuracionDelDia_DejaPertenenciaNula_CuandoElDiaEsDescanso()
    {
        var dia = Hidratar("Descanso", [], [new EventoMarcacionDelDia(Entrada, "Entrada")]);

        dia.GenerarDepuracionDelDia().Marcaciones.Should().ContainSingle().Which.Pertenencia.Should().BeNull();
    }

    [Fact]
    public void GenerarDepuracionDelDia_DejaPertenenciaNula_CuandoNoHayTurno()
    {
        var dia = Hidratar(null, [], [new EventoMarcacionDelDia(Entrada, "Entrada")]);

        dia.GenerarDepuracionDelDia().Marcaciones.Should().ContainSingle().Which.Pertenencia.Should().BeNull();
    }

    [Fact]
    public void GenerarDepuracionDelDia_DejaPertenenciaNula_CuandoHayAusencia()
    {
        var dia = Hidratar(null, [], [new EventoMarcacionDelDia(Entrada, "Entrada")], "Vacaciones");

        dia.GenerarDepuracionDelDia().Marcaciones.Should().ContainSingle().Which.Pertenencia.Should().BeNull();
    }

    [Fact]
    public void GenerarDepuracionDelDia_ExponeDeTurnoVecinoYDejaNulaLaDemas_CuandoElDiaEsDescanso()
    {
        var dia = Hidratar("Descanso", [],
        [
            new EventoMarcacionDelDia(Madrugada, "Salida", Pertenencia: "DeTurnoVecino"),
            new EventoMarcacionDelDia(Entrada, "Entrada")
        ]);

        dia.GenerarDepuracionDelDia().Marcaciones.Select(m => m.Pertenencia).Should().Equal(
            VistaPertenencia.DeTurnoVecino, null);
    }

    [Fact]
    public void GenerarDepuracionDelDia_ExponeDeTurnoVecinoYDejaNulaLaDemas_CuandoNoHayTurno()
    {
        var dia = Hidratar(null, [],
        [
            new EventoMarcacionDelDia(Madrugada, "Salida", Pertenencia: "DeTurnoVecino"),
            new EventoMarcacionDelDia(Entrada, "Entrada")
        ]);

        dia.GenerarDepuracionDelDia().Marcaciones.Select(m => m.Pertenencia).Should().Equal(
            VistaPertenencia.DeTurnoVecino, null);
    }

    [Fact]
    public void GenerarDepuracionDelDia_ExponeDeTurnoVecinoYDejaNulaLaDemas_CuandoHayAusencia()
    {
        var dia = Hidratar(null, [],
        [
            new EventoMarcacionDelDia(Madrugada, "Salida", Pertenencia: "DeTurnoVecino"),
            new EventoMarcacionDelDia(Entrada, "Entrada")
        ], "Vacaciones");

        dia.GenerarDepuracionDelDia().Marcaciones.Select(m => m.Pertenencia).Should().Equal(
            VistaPertenencia.DeTurnoVecino, null);
    }

    // La vista lee por nombre el string del evento: un valor del dominio que falte aqui se leeria
    // como DelTurno sin error.
    [Fact]
    public void PertenenciaMarcacion_TieneLosMismosNombresEnLaVistaQueEnElDominio()
    {
        Enum.GetNames<VistaPertenencia>().Should().Equal(Enum.GetNames<PertenenciaMarcacion>());
    }
}
