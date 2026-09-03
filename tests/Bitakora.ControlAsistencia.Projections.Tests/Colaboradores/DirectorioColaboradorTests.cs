// Las estaticas de la vista se testean desde Projections.Tests porque ReadModels es una isla sin
// referencias de proyecto y no tiene proyecto de tests propio (CA-ADR-0029).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;

namespace Bitakora.ControlAsistencia.Projections.Tests.Colaboradores;

public class DirectorioColaboradorTests
{
    [Fact]
    public void TokenizarNombre_QuitaDiacriticosYPasaAMinusculas()
    {
        var tokens = DirectorioColaborador.TokenizarNombre("Juan Pablo Bermúdez");

        tokens.Should().BeEquivalentTo(["juan", "pablo", "bermudez"], o => o.WithStrictOrdering());
    }

    [Fact]
    public void TokenizarNombre_DescartaEspaciosSobrantes()
    {
        var tokens = DirectorioColaborador.TokenizarNombre("  María  José ");

        tokens.Should().BeEquivalentTo(["maria", "jose"], o => o.WithStrictOrdering());
    }

    // La enie colapsa junto al resto de los diacriticos: "Munoz" y "Muñoz" son el mismo token.
    [Fact]
    public void TokenizarNombre_DescartaTokensDuplicados()
    {
        var tokens = DirectorioColaborador.TokenizarNombre("Ana Ana Muñoz");

        tokens.Should().BeEquivalentTo(["ana", "munoz"], o => o.WithStrictOrdering());
    }

    [Fact]
    public void TokenizarNombre_SeparaPorTodoCaracterQueNoSeaLetraNiDigito()
    {
        var tokens = DirectorioColaborador.TokenizarNombre("[TEST] García-Márquez");

        tokens.Should().BeEquivalentTo(["test", "garcia", "marquez"], o => o.WithStrictOrdering());
    }

    // Misma regla que Identificacion.Crear (Colaboradores.DomainEvents): un numero escrito con
    // puntos o guiones tiene que encontrar la misma entrada que el numero limpio.
    [Fact]
    public void NormalizarNumeroDocumento_ConservaSoloLetrasYDigitosEnMayusculas()
    {
        var numero = DirectorioColaborador.NormalizarNumeroDocumento(" ab-123.456 ");

        numero.Should().Be("AB123456");
    }
}
