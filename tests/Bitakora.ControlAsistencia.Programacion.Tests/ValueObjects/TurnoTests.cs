using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class TurnoTests
{
    private static FranjaOrdinaria Manana() =>
        FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

    private static FranjaOrdinaria Tarde() =>
        FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

    private static FranjaOrdinaria Solapada() =>
        FranjaOrdinaria.Crear(new TimeOnly(12, 0), new TimeOnly(16, 0));

    private static Turno TurnoDeTrabajo() => Turno.Crear("Turno Manana", false, [Manana()]);

    [Fact]
    public void EstaCompleto_RetornaTrue_CuandoEsDescanso()
    {
        var turno = Turno.Crear("Descanso", true, []);

        turno.EstaCompleto().Should().BeTrue();
    }

    [Fact]
    public void EstaCompleto_RetornaTrue_CuandoTieneAlMenosUnaFranja()
    {
        TurnoDeTrabajo().EstaCompleto().Should().BeTrue();
    }

    [Fact]
    public void EstaCompleto_RetornaFalse_CuandoNoEsDescansoYNoTieneFranjas()
    {
        Turno.Crear("Vacio", false, []).EstaCompleto().Should().BeFalse();
    }

    [Fact]
    public void ToString_DescribeFranjasConDescansoYExtra_CuandoEsTurnoDeTrabajo()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))
            .ConDescanso(new TimeOnly(10, 0), new TimeOnly(10, 30))
            .ConExtra(new TimeOnly(12, 0), new TimeOnly(14, 0));
        var turno = Turno.Crear("Turno Manana", false, [franja]);

        turno.ToString().Should().Be(
            "06:00-14:00, descanso 10:00-10:30, extra 12:00-14:00");
    }

    [Fact]
    public void ToString_UneLasFranjasConComa_SinElNombre_CuandoTieneVariasFranjas()
    {
        var turno = Turno.Crear("Turno partido", false,
        [
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(10, 0)),
            FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(18, 0))
        ]);

        turno.ToString().Should().Be("06:00-10:00, 14:00-18:00");
    }

    [Fact]
    public void ToString_DescribeDescansoYExtraConOffset_SinSede_CuandoLaFranjaCruzaMedianoche()
    {
        var ocoa = new SedeProgramada("SEDE-OCOA", "Ocoa");
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), sede: ocoa)
            .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30));
        // El extra debe quedar contenido en la ordinaria, asi que esta termina a las 07:00+1.
        var franjaConExtra = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(7, 0), sede: ocoa)
            .ConDescanso(new TimeOnly(2, 0), new TimeOnly(2, 30))
            .ConExtra(new TimeOnly(6, 0), new TimeOnly(7, 0));

        franja.ToString().Should().Be("22:00-06:00+1, descanso 02:00+1-02:30+1");
        franjaConExtra.ToString().Should().Be("22:00-07:00+1, descanso 02:00+1-02:30+1, extra 06:00+1-07:00+1");
    }

    [Fact]
    public void ToString_AgregaLabelDescanso_CuandoEsDescanso()
    {
        Turno.Crear("Libre", true, []).ToString()
            .Should().Be("(descanso)");
    }

    [Fact]
    public void ToString_AgregaLabelIncompleto_CuandoNoTieneFranjas()
    {
        Turno.Crear("Nuevo", false, []).ToString()
            .Should().Be("(incompleto)");
    }

    [Fact]
    public void Mensajes_ConservanTextoHistorico_EnLaResxDelVo()
    {
        Turno.Mensajes.LabelDescanso.Should().Be("(descanso)");
        Turno.Mensajes.LabelIncompleto.Should().Be("(incompleto)");
    }

    [Fact]
    public void Programar_ProduceTurnoProgramadoConNombreFranjasYDescripcion()
    {
        var turno = Turno.Crear("Doble", false, [Manana(), Tarde()]);

        var programado = turno.Programar();

        programado.Nombre.Should().Be("Doble");
        programado.FranjasOrdinarias.Should().HaveCount(2);
        programado.Descripcion.Should().Be("06:00-14:00, 14:00-22:00");
    }

    [Fact]
    public void Programar_CopiaLaAmplitudDeLaVentanaEnMinutos_CuandoElTurnoTieneVentanaDeEntrada()
    {
        var turno = TurnoDeTrabajo().ConVentanaDeEntrada(HorasYMinutos.Crear(3, 0));

        turno.Programar().AmplitudVentanaEntradaEnMinutos.Should().Be(180);
    }

    [Fact]
    public void Programar_DejaAmplitudEnCero_CuandoElTurnoEsFijo()
    {
        TurnoDeTrabajo().Programar().AmplitudVentanaEntradaEnMinutos.Should().Be(0);
    }

    [Fact]
    public void FranjaQueEmpiezaA_RetornaLaFranja_CuandoUnaFranjaEmpiezaEsaHora()
    {
        TurnoDeTrabajo().FranjaQueEmpiezaA(new TimeOnly(6, 0)).Should().Be(Manana());
    }

    [Fact]
    public void FranjaQueEmpiezaA_RetornaNull_CuandoNingunaFranjaEmpiezaEsaHora()
    {
        TurnoDeTrabajo().FranjaQueEmpiezaA(new TimeOnly(7, 0)).Should().BeNull();
    }

    [Fact]
    public void SeSolapaCon_RetornaTrue_CuandoLaFranjaInvadeUnaExistente()
    {
        TurnoDeTrabajo().SeSolapaCon(Solapada()).Should().BeTrue();
    }

    [Fact]
    public void SeSolapaCon_RetornaFalse_CuandoLaFranjaEsContigua()
    {
        TurnoDeTrabajo().SeSolapaCon(Tarde()).Should().BeFalse();
    }

    [Fact]
    public void ConFranja_RetornaTurnoNuevoConLaFranja_SinMutarElOriginal()
    {
        var original = TurnoDeTrabajo();

        var resultado = original.ConFranja(Tarde());

        resultado.ToString().Should().Be("06:00-14:00, 14:00-22:00");
        original.ToString().Should().Be("06:00-14:00");
    }

    [Fact]
    public void SinFranjaQueEmpiezaA_RetiraLaFranja_CuandoExiste()
    {
        var turno = Turno.Crear("Doble", false, [Manana(), Tarde()]);

        var resultado = turno.SinFranjaQueEmpiezaA(Manana());

        resultado.ToString().Should().Be("14:00-22:00");
    }

    [Fact]
    public void SinFranjaQueEmpiezaA_RetornaTurnoIgual_CuandoLaFranjaNoExiste()
    {
        var turno = TurnoDeTrabajo();

        var resultado = turno.SinFranjaQueEmpiezaA(Tarde());

        resultado.Should().Be(turno);
    }

    [Fact]
    public void ConFranjaReemplazada_ReemplazaLaFranjaDeLaMismaHoraDeInicio()
    {
        var conDescanso = Manana().ConDescanso(new TimeOnly(10, 0), new TimeOnly(10, 30));

        var resultado = TurnoDeTrabajo().ConFranjaReemplazada(conDescanso);

        resultado.FranjaQueEmpiezaA(new TimeOnly(6, 0)).Should().Be(conDescanso);
    }

    [Fact]
    public void ConFranjaReemplazada_RetornaTurnoIgual_CuandoNingunaFranjaEmpiezaEsaHora()
    {
        var turno = TurnoDeTrabajo();

        var resultado = turno.ConFranjaReemplazada(Tarde());

        resultado.Should().Be(turno);
    }
}
