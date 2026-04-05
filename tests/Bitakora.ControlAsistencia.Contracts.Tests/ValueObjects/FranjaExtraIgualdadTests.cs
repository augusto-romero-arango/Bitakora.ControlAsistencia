// Issue #2: Tests de igualdad por valor para FranjaExtra
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaExtraIgualdadTests
{
    // ---------- Equals(FranjaExtra?) ----------

    [Fact]
    public void Equals_RetornaTrue_CuandoMismosValores()
    {
        var a = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var b = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_RetornaTrue_CuandoMismosValoresConOffsets()
    {
        var a = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);
        var b = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoHoraInicioDiferente()
    {
        var a = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var b = FranjaExtra.Crear(new TimeOnly(7, 0), new TimeOnly(8, 0));

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoHoraFinDiferente()
    {
        var a = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var b = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(9, 0));

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoOffsetInicioDiferente()
    {
        var a = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var b = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 1, diaOffsetFin: 0);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoOffsetFinDiferente()
    {
        var a = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var b = FranjaExtra.Crear(new TimeOnly(4, 0), new TimeOnly(6, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoOtroEsNull()
    {
        var a = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        a.Equals((FranjaExtra?)null).Should().BeFalse();
    }

    // ---------- Equals(object?) ----------

    [Fact]
    public void EqualsObject_RetornaTrue_CuandoMismoValorComoObject()
    {
        var a = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        object b = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsObject_RetornaFalse_CuandoTipoDiferente()
    {
        var a = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        a.Equals("no es franja").Should().BeFalse();
    }

    [Fact]
    public void EqualsObject_RetornaFalse_CuandoObjectNull()
    {
        var a = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        a.Equals((object?)null).Should().BeFalse();
    }

    // ---------- GetHashCode ----------

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoMismosValores()
    {
        var a = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var b = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_RetornaHashDiferente_CuandoValoresDiferentes()
    {
        var a = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var b = FranjaExtra.Crear(new TimeOnly(18, 0), new TimeOnly(20, 0));

        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }
}
