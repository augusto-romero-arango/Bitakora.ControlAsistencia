using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarMallaSemanalTurnos;

public class PaginaDeMallaSemanalTests
{
    private static ColaboradorVigente Colab(string codigo) => new(codigo, new DateOnly(2027, 1, 1), null);

    [Fact]
    public void AcotarTake_ConservaNull_CuandoNoHayTake() =>
        PaginaDeMallaSemanal.AcotarTake(null).Should().BeNull();

    [Fact]
    public void AcotarTake_AcotaAUno_CuandoTakeEsCero() =>
        PaginaDeMallaSemanal.AcotarTake(0).Should().Be(1);

    [Fact]
    public void AcotarTake_AcotaA200_CuandoTakeEs500() =>
        PaginaDeMallaSemanal.AcotarTake(500).Should().Be(200);

    [Fact]
    public void PosterioresAlCursor_DescartaLosCodigosHastaElCursor_ConOrdenOrdinal()
    {
        var resultado = PaginaDeMallaSemanal.PosterioresAlCursor([Colab("A"), Colab("B"), Colab("C")], "B");

        resultado.Select(c => c.CodigoColaborador).Should().Equal("C");
    }

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var (elementos, cursor) = PaginaDeMallaSemanal.Cortar(new[] { "A", "B", "C" }, null, x => x);

        elementos.Should().Equal("A", "B", "C");
        cursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_EmiteCursorDelUltimoDeLaPagina_CuandoQuedanMas()
    {
        var (elementos, cursor) = PaginaDeMallaSemanal.Cortar(new[] { "A", "B", "C" }, 2, x => x);

        elementos.Should().Equal("A", "B");
        CursorOpaco.TryDecodificar(cursor!, out var codigo).Should().BeTrue();
        codigo.Should().Be("B");
    }

    [Fact]
    public void Cortar_NoEmiteCursor_CuandoLaPaginaAgotaLaLista()
    {
        var (elementos, cursor) = PaginaDeMallaSemanal.Cortar(new[] { "C" }, 2, x => x);

        elementos.Should().Equal("C");
        cursor.Should().BeNull();
    }
}
