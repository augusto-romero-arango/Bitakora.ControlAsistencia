// Issue #2: Tests de igualdad por valor para FranjaDescanso
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class FranjaDescansoIgualdadTests
{
    // ---------- Equals(FranjaDescanso?) ----------

    [Fact]
    public void Equals_RetornaTrue_CuandoMismosValores()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var b = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_RetornaTrue_CuandoMismosValoresConOffsets()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(23, 50), new TimeOnly(0, 10),
            diaOffsetInicio: 0, diaOffsetFin: 1);
        var b = FranjaDescanso.Crear(new TimeOnly(23, 50), new TimeOnly(0, 10),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoHoraInicioDiferente()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var b = FranjaDescanso.Crear(new TimeOnly(10, 5), new TimeOnly(10, 15));

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoHoraFinDiferente()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var b = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30));

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoOffsetInicioDiferente()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(1, 0), new TimeOnly(1, 15),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var b = FranjaDescanso.Crear(new TimeOnly(1, 0), new TimeOnly(1, 15),
            diaOffsetInicio: 1, diaOffsetFin: 0);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoOffsetFinDiferente()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(23, 50), new TimeOnly(0, 10),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var b = FranjaDescanso.Crear(new TimeOnly(23, 50), new TimeOnly(0, 10),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoOtroEsNull()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        a.Equals((FranjaDescanso?)null).Should().BeFalse();
    }

    // ---------- Equals(object?) ----------

    [Fact]
    public void EqualsObject_RetornaTrue_CuandoMismoValorComoObject()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        object b = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsObject_RetornaFalse_CuandoTipoDiferente()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        a.Equals("no es franja").Should().BeFalse();
    }

    [Fact]
    public void EqualsObject_RetornaFalse_CuandoObjectNull()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        a.Equals((object?)null).Should().BeFalse();
    }

    // ---------- GetHashCode ----------

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoMismosValores()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var b = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_RetornaHashDiferente_CuandoValoresDiferentes()
    {
        var a = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var b = FranjaDescanso.Crear(new TimeOnly(11, 0), new TimeOnly(11, 30));

        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }
}
