// La HorasDiscriminadas de ControlHoras.DomainEvents (hermana de la de PrivateEvents) sobrescribe
// Equals/GetHashCode para comparar HorasPorConcepto y Trazabilidad por valor: sin estos tests, un
// cambio en esos overrides solo se veria al comparar dos DepuracionDiaRecibida.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionCuandoDiaDepurado.Eventos;

public class HorasDiscriminadasIgualdadTests : IgualdadTestBase<HorasDiscriminadas>
{
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

    // El hash del diccionario se acumula con XOR justamente para no depender del orden de
    // enumeracion: dos instancias iguales con distinto orden de insercion deben coincidir.
    [Fact]
    public void GetHashCode_RetornaMismoHash_CuandoElOrdenDeInsercionDifiere()
    {
        var a = new HorasDiscriminadas(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 7.00m, ["Retardo"] = 0.25m }, []);
        var b = new HorasDiscriminadas(
            new Dictionary<string, decimal> { ["Retardo"] = 0.25m, ["OrdinariaDiurna"] = 7.00m }, []);

        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Equals(b).Should().BeTrue();
    }
}
