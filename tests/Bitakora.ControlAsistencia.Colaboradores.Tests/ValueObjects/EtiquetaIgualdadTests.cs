// HU-353 CA-3: igualdad por valor COMPLETA de Etiqueta (categoria normalizada + valor
// normalizado). Decision discutida y confirmada con el usuario (sesion 2026-08-11): Equals
// compara ambos campos -- dos etiquetas de la misma categoria con valor distinto NO son iguales.
// EsMismaCategoria (superficie separada para #355) se testea en EtiquetaTests.cs.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

/// <summary>
/// Hereda los 8 tests de IgualdadTestBase que verifican el contrato IEquatable completo.
/// CrearInstancia/CrearInstanciaCopia usan variaciones de tildes/mayusculas de la MISMA etiqueta;
/// CrearInstanciasDiferentes varia categoria y valor por separado.
/// </summary>
public class EtiquetaIgualdadTests : IgualdadTestBase<Etiqueta>
{
    protected override Etiqueta CrearInstancia() =>
        Etiqueta.Crear("Área", "Tecnología");

    protected override Etiqueta CrearInstanciaCopia() =>
        Etiqueta.Crear("area", "TECNOLOGIA");

    protected override IEnumerable<(string, Etiqueta)> CrearInstanciasDiferentes()
    {
        yield return ("Categoria", Etiqueta.Crear("Sede", "Tecnologia"));
        yield return ("Valor", Etiqueta.Crear("Area", "Ventas"));
    }

    // CA-3 explicito: las tres formas de escribir la misma etiqueta son iguales entre si (no solo
    // pares) y su GetHashCode coincide en las tres.
    [Fact]
    public void Equals_RetornaTrue_ConTresVariantesDeEscrituraDeLaMismaEtiqueta()
    {
        var conTildesYMixta = Etiqueta.Crear("Área", "Tecnología");
        var minusculas = Etiqueta.Crear("area", "tecnologia");
        var mayusculas = Etiqueta.Crear("AREA", "TECNOLOGIA");

        conTildesYMixta.Should().Be(minusculas);
        conTildesYMixta.Should().Be(mayusculas);
        minusculas.Should().Be(mayusculas);
        conTildesYMixta.GetHashCode().Should().Be(minusculas.GetHashCode());
        conTildesYMixta.GetHashCode().Should().Be(mayusculas.GetHashCode());
    }

    // CA-3 explicito: misma categoria, valor distinto -> NO son iguales.
    [Fact]
    public void Equals_RetornaFalse_CuandoMismaCategoriaConValorDistinto()
    {
        var areaVentas = Etiqueta.Crear("area", "ventas");
        var areaTecnologia = Etiqueta.Crear("area", "tecnologia");

        areaVentas.Should().NotBe(areaTecnologia);
    }
}
