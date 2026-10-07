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
            $"Turno Manana (06:00-14:00)[{FranjaTemporal.Mensajes.LabelDescansos}:(10:00-10:30)]"
            + $"[{FranjaTemporal.Mensajes.LabelExtras}:(12:00-14:00)]");
    }

    [Fact]
    public void ToString_AgregaLabelDescanso_CuandoEsDescanso()
    {
        Turno.Crear("Libre", true, []).ToString()
            .Should().Be($"Libre {Turno.Mensajes.LabelDescanso}");
    }

    [Fact]
    public void ToString_AgregaLabelIncompleto_CuandoNoTieneFranjas()
    {
        Turno.Crear("Nuevo", false, []).ToString()
            .Should().Be($"Nuevo {Turno.Mensajes.LabelIncompleto}");
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
        programado.Descripcion.Should().Be("Doble (06:00-14:00)(14:00-22:00)");
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

        resultado.ToString().Should().Be("Turno Manana (06:00-14:00)(14:00-22:00)");
        original.ToString().Should().Be("Turno Manana (06:00-14:00)");
    }

    [Fact]
    public void SinFranjaQueEmpiezaA_RetiraLaFranja_CuandoExiste()
    {
        var turno = Turno.Crear("Doble", false, [Manana(), Tarde()]);

        var resultado = turno.SinFranjaQueEmpiezaA(Manana());

        resultado.ToString().Should().Be("Doble (14:00-22:00)");
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
