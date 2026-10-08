using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAdvertenciasProgramacionSemanal;

public class PaginaDeAdvertenciasTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(500, 200)]
    public void AcotarTake_AcotaA1_200_YConservaNull(int? take, int? esperado) =>
        PaginaDeAdvertencias.AcotarTake(take).Should().Be(esperado);

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var (elementos, cursor) = PaginaDeAdvertencias.Cortar(["A", "B"], null, c => c);

        elementos.Should().Equal("A", "B");
        cursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelveCursorOpacoDelUltimo_CuandoHayMasQueTake()
    {
        var (elementos, cursor) = PaginaDeAdvertencias.Cortar(["A", "B"], 1, c => c);

        elementos.Should().Equal("A");
        CursorOpaco.TryDecodificar(cursor!, out var codigo).Should().BeTrue();
        codigo.Should().Be("A");
    }

    [Fact]
    public void Cortar_OmiteCursor_CuandoLasFilasCabenEnTake()
    {
        var (elementos, cursor) = PaginaDeAdvertencias.Cortar(["B"], 1, c => c);

        elementos.Should().Equal("B");
        cursor.Should().BeNull();
    }
}
