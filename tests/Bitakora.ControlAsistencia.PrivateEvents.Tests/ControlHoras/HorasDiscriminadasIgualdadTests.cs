// HorasDiscriminadas sobreescribe Equals/GetHashCode para comparar HorasPorConcepto y Trazabilidad
// por valor: el record por defecto compararia sus colecciones por referencia.
// HorasPorConcepto habla horas liquidables (decimal).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

public class HorasDiscriminadasIgualdadTests : IgualdadTestBase<HorasDiscriminadas>
{
    // Orden de insercion consistente entre instancia y copia para que la comparacion no dependa
    // del orden de enumeracion del diccionario.
    private static Dictionary<string, decimal> Horas() => new()
    {
        ["OrdinariaDiurna"] = 7.00m,
        ["Retardo"] = 0.25m
    };

    protected override HorasDiscriminadas CrearInstancia() =>
        new(Horas(), ["entro 06:15, retardo 15min"]);

    protected override HorasDiscriminadas CrearInstanciaCopia() =>
        new(Horas(), ["entro 06:15, retardo 15min"]);

    protected override IEnumerable<(string, HorasDiscriminadas)> CrearInstanciasDiferentes()
    {
        yield return ("HorasPorConcepto (valor distinto)",
            new HorasDiscriminadas(
                new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 9.99m, ["Retardo"] = 0.25m },
                ["entro 06:15, retardo 15min"]));
        yield return ("HorasPorConcepto (clave distinta)",
            new HorasDiscriminadas(
                new Dictionary<string, decimal> { ["OrdinariaNocturna"] = 7.00m, ["Retardo"] = 0.25m },
                ["entro 06:15, retardo 15min"]));
        yield return ("Trazabilidad",
            new HorasDiscriminadas(Horas(), ["otra nota"]));
    }

    [Fact]
    public void Equals_RetornaTrue_CuandoColeccionesSonInstanciasDiferentesConMismoContenido()
    {
        var a = new HorasDiscriminadas(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 7.00m }, ["nota"]);
        var b = new HorasDiscriminadas(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 7.00m }, ["nota"]);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoColeccionesSonInstanciasDiferentesConMismoContenido()
    {
        var a = new HorasDiscriminadas(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 7.00m }, ["nota"]);
        var b = new HorasDiscriminadas(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 7.00m }, ["nota"]);

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
