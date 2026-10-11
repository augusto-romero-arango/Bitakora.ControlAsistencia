using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarDiasSinProgramar;

public class PaginaDeDiasSinProgramarTests
{
    [Fact]
    public void AcotarTake_ConservaNull_CuandoNoHayTake() =>
        PaginaDeDiasSinProgramar.AcotarTake(null).Should().BeNull();

    [Fact]
    public void AcotarTake_AcotaAUno_CuandoTakeEsCero() =>
        PaginaDeDiasSinProgramar.AcotarTake(0).Should().Be(1);

    [Fact]
    public void AcotarTake_AcotaA200_CuandoTakeEs500() =>
        PaginaDeDiasSinProgramar.AcotarTake(500).Should().Be(200);

    [Fact]
    public void AcotarTake_ConservaElValor_CuandoEstaEnRango() =>
        PaginaDeDiasSinProgramar.AcotarTake(50).Should().Be(50);

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var (elementos, cursor) = PaginaDeDiasSinProgramar.Cortar(new[] { "A", "C", "D" }, null, x => x);

        elementos.Should().Equal("A", "C", "D");
        cursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelvePaginaYCursorDelUltimo_CuandoHayMasFilasQueTake()
    {
        var (elementos, cursor) = PaginaDeDiasSinProgramar.Cortar(new[] { "A", "C", "D" }, 2, x => x);

        elementos.Should().Equal("A", "C");
        CursorOpaco.TryDecodificar(cursor!, out var codigo).Should().BeTrue();
        codigo.Should().Be("C");
    }

    [Fact]
    public void Cortar_NoDevuelveCursor_CuandoLasFilasCabenEnLaPagina()
    {
        var (elementos, cursor) = PaginaDeDiasSinProgramar.Cortar(new[] { "D" }, 2, x => x);

        elementos.Should().Equal("D");
        cursor.Should().BeNull();
    }
}
