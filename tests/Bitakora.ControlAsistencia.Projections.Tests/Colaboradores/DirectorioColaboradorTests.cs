// Issue #587 CA-4: unit tests de las estaticas de DirectorioColaborador (TokenizarNombre,
// NormalizarNumeroDocumento). Viven en Projections.Tests porque no existe proyecto de tests propio
// de ReadModels (issue #587, "Capas de test esperadas" -- propuesta explicitamente revisable, aqui
// mantenida). MEF-ADR-0012 (Tell-don't-Ask): la vista expone estos dos metodos porque los usan DOS
// procesos que no se referencian entre si -- DirectorioColaboradorProjection (al escribir) y el
// endpoint de #590 (al leer, para tokenizar/normalizar el termino de busqueda del cliente) -- asi
// que ningun algoritmo de normalizacion se duplica entre ambos.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;

namespace Bitakora.ControlAsistencia.Projections.Tests.Colaboradores;

public class DirectorioColaboradorTests
{
    // Casos literales del issue #587 CA-4: separa por todo caracter que no sea letra ni digito
    // (espacios, guiones, puntos, corchetes), sin diacriticos, minusculas, sin vacios ni duplicados,
    // en orden de aparicion.
    [Theory]
    [InlineData("Juan Pablo Bermúdez", new[] { "juan", "pablo", "bermudez" })]
    [InlineData("  María  José ", new[] { "maria", "jose" })]
    [InlineData("Ana Ana Muñoz", new[] { "ana", "munoz" })]
    [InlineData("[TEST] García-Márquez", new[] { "test", "garcia", "marquez" })]
    public void TokenizarNombre_NormalizaSeparaYDeduplica_SegunElCaso(string nombre, string[] tokensEsperados)
    {
        var tokens = DirectorioColaborador.TokenizarNombre(nombre);

        tokens.Should().BeEquivalentTo(tokensEsperados, o => o.WithStrictOrdering());
    }

    // Caso literal del issue #587 CA-4: misma regla que Identificacion.Crear (Colaboradores.
    // DomainEvents) -- conserva solo [A-Za-z0-9], mayusculas invariantes.
    [Fact]
    public void NormalizarNumeroDocumento_ConservaSoloLetrasYDigitosEnMayusculas()
    {
        var numero = DirectorioColaborador.NormalizarNumeroDocumento(" ab-123.456 ");

        numero.Should().Be("AB123456");
    }
}
