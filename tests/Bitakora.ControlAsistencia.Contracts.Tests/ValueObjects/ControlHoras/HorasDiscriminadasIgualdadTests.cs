// Issue #183: Tests de contrato IEquatable para HorasDiscriminadas.
// HorasDiscriminadas es record con override manual de Equals/GetHashCode que compara
// MinutosPorConcepto y Trazabilidad por valor (en lugar de la igualdad por referencia que el record
// genera por defecto - precedente DetalleFranjaOrdinaria, #129; ADR-0015 advierte sobre records con
// colecciones que prometen igualdad por valor que no cumplen).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

public class HorasDiscriminadasIgualdadTests : IgualdadTestBase<HorasDiscriminadas>
{
    // Orden de insercion consistente entre instancia y copia para que la comparacion no dependa
    // del orden de enumeracion del diccionario.
    private static Dictionary<string, int> Minutos() => new()
    {
        ["OrdinariaDiurna"] = 420,
        ["Retardo"] = 15
    };

    protected override HorasDiscriminadas CrearInstancia() =>
        new(Minutos(), ["entro 06:15, retardo 15min"]);

    protected override HorasDiscriminadas CrearInstanciaCopia() =>
        new(Minutos(), ["entro 06:15, retardo 15min"]);

    protected override IEnumerable<(string, HorasDiscriminadas)> CrearInstanciasDiferentes()
    {
        yield return ("MinutosPorConcepto (valor distinto)",
            new HorasDiscriminadas(
                new Dictionary<string, int> { ["OrdinariaDiurna"] = 999, ["Retardo"] = 15 },
                ["entro 06:15, retardo 15min"]));
        yield return ("MinutosPorConcepto (clave distinta)",
            new HorasDiscriminadas(
                new Dictionary<string, int> { ["OrdinariaNocturna"] = 420, ["Retardo"] = 15 },
                ["entro 06:15, retardo 15min"]));
        yield return ("Trazabilidad",
            new HorasDiscriminadas(Minutos(), ["otra nota"]));
    }

    // Cobertura especifica del override: las colecciones se comparan por valor, no por referencia.

    [Fact]
    public void Equals_RetornaTrue_CuandoColeccionesSonInstanciasDiferentesConMismoContenido()
    {
        var a = new HorasDiscriminadas(
            new Dictionary<string, int> { ["OrdinariaDiurna"] = 420 }, ["nota"]);
        var b = new HorasDiscriminadas(
            new Dictionary<string, int> { ["OrdinariaDiurna"] = 420 }, ["nota"]);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoColeccionesSonInstanciasDiferentesConMismoContenido()
    {
        var a = new HorasDiscriminadas(
            new Dictionary<string, int> { ["OrdinariaDiurna"] = 420 }, ["nota"]);
        var b = new HorasDiscriminadas(
            new Dictionary<string, int> { ["OrdinariaDiurna"] = 420 }, ["nota"]);

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
