// Issue #2: Tests de igualdad por valor para FranjaOrdinaria
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaOrdinariaIgualdadTests
{
    // ---------- Equals(FranjaOrdinaria?) ----------

    [Fact]
    public void Equals_RetornaTrue_CuandoMismosValoresSinHijos()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_RetornaTrue_CuandoMismosValoresConHijos()
    {
        var descansoA = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var extraA = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descansoA], extras: [extraA]);

        var descansoB = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var extraB = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descansoB], extras: [extraB]);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_RetornaTrue_CuandoMismosValoresConOffset()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);
        var b = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoHoraInicioDiferente()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var b = FranjaOrdinaria.Crear(new TimeOnly(7, 0), new TimeOnly(12, 0));

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoHoraFinDiferente()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoOffsetFinDiferente()
    {
        // Ambas con horaFin > horaInicio para evitar inferencia automatica de offset
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0), diaOffsetFin: 0);
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0), diaOffsetFin: 1);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoDescansosDiferentes()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))]);
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30))]);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoExtrasDiferentes()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            extras: [FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(7, 0))]);
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            extras: [FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0))]);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoUnoTieneHijosYOtroNo()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))]);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoOtroEsNull()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        a.Equals((FranjaOrdinaria?)null).Should().BeFalse();
    }

    // ---------- Equals(object?) ----------

    [Fact]
    public void EqualsObject_RetornaTrue_CuandoMismoValorComoObject()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        object b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsObject_RetornaFalse_CuandoTipoDiferente()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        a.Equals("no es franja").Should().BeFalse();
    }

    [Fact]
    public void EqualsObject_RetornaFalse_CuandoObjectNull()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        a.Equals((object?)null).Should().BeFalse();
    }

    // ---------- GetHashCode ----------

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoMismosValores()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoMismosValoresConHijos()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))]);
        var b = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15))]);

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_RetornaHashDiferente_CuandoValoresDiferentes()
    {
        var a = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));
        var b = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }
}
