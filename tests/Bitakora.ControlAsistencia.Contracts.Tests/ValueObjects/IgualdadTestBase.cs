using AwesomeAssertions;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

/// <summary>
/// Clase base generica para tests de contrato IEquatable en value objects.
/// Subclases solo definen las instancias de prueba; los 8 tests se heredan.
/// </summary>
public abstract class IgualdadTestBase<T> where T : class, IEquatable<T>
{
    protected abstract T CrearInstancia();
    protected abstract T CrearInstanciaCopia();
    protected abstract IEnumerable<(string atributo, T diferente)> CrearInstanciasDiferentes();

    [Fact]
    public void Equals_RetornaTrue_CuandoMismosValores()
    {
        var a = CrearInstancia();
        var b = CrearInstanciaCopia();

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoAtributoDiferente()
    {
        var instancia = CrearInstancia();

        foreach (var (atributo, diferente) in CrearInstanciasDiferentes())
        {
            instancia.Equals(diferente).Should().BeFalse(
                $"Equals deberia retornar false cuando '{atributo}' es diferente");
        }
    }

    [Fact]
    public void Equals_RetornaFalse_CuandoOtroEsNull()
    {
        var a = CrearInstancia();

        a.Equals((T?)null).Should().BeFalse();
    }

    [Fact]
    public void EqualsObject_RetornaTrue_CuandoMismoValorComoObject()
    {
        var a = CrearInstancia();
        object b = CrearInstanciaCopia();

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsObject_RetornaFalse_CuandoTipoDiferente()
    {
        var a = CrearInstancia();

        a.Equals("no es el tipo correcto").Should().BeFalse();
    }

    [Fact]
    public void EqualsObject_RetornaFalse_CuandoObjectNull()
    {
        var a = CrearInstancia();

        a.Equals((object?)null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoMismosValores()
    {
        var a = CrearInstancia();
        var b = CrearInstanciaCopia();

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_RetornaHashDiferente_CuandoValoresDiferentes()
    {
        var a = CrearInstancia();
        var b = CrearInstanciasDiferentes().First().diferente;

        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }
}
