// Equals compara categoria y valor normalizados (decision confirmada con el usuario): misma categoria
// con valor distinto no es igual. EsMismaCategoria se prueba en EtiquetaTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

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

    [Fact]
    public void Equals_RetornaTrue_CuandoLasTresVariantesDeEscrituraSonDeLaMismaEtiqueta()
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

    [Fact]
    public void Equals_RetornaFalse_CuandoMismaCategoriaConValorDistinto()
    {
        var areaVentas = Etiqueta.Crear("area", "ventas");
        var areaTecnologia = Etiqueta.Crear("area", "tecnologia");

        areaVentas.Should().NotBe(areaTecnologia);
    }
}
