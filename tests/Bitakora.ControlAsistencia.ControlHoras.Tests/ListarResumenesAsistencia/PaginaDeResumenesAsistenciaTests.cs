using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarResumenesAsistencia;

public class PaginaDeResumenesAsistenciaTests
{
    [Fact]
    public void TakeEnRango_AceptaNullYLosLimites()
    {
        PaginaDeResumenesAsistencia.TakeEnRango(null).Should().BeTrue();
        PaginaDeResumenesAsistencia.TakeEnRango(1).Should().BeTrue();
        PaginaDeResumenesAsistencia.TakeEnRango(200).Should().BeTrue();
    }

    [Fact]
    public void TakeEnRango_RechazaCeroNegativoYMasDe200()
    {
        PaginaDeResumenesAsistencia.TakeEnRango(0).Should().BeFalse();
        PaginaDeResumenesAsistencia.TakeEnRango(-1).Should().BeFalse();
        PaginaDeResumenesAsistencia.TakeEnRango(201).Should().BeFalse();
    }

    [Fact]
    public void PosterioresAlCursor_OrdenaOrdinalYColapsaRepetidos_CuandoNoHayCursor() =>
        PaginaDeResumenesAsistencia.PosterioresAlCursor(["EMP-C", "EMP-A", "EMP-A", "EMP-B"], null)
            .Should().Equal("EMP-A", "EMP-B", "EMP-C");

    [Fact]
    public void PosterioresAlCursor_ExcluyeElCodigoDelCursor_CuandoElCursorCoincideConUnCodigo() =>
        PaginaDeResumenesAsistencia.PosterioresAlCursor(["EMP-A", "EMP-B", "EMP-C"], "EMP-B")
            .Should().Equal("EMP-C");

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var (elementos, cursor) = PaginaDeResumenesAsistencia.Cortar(new[] { "A", "B", "C" }, null, x => CursorOpaco.Codificar(x));

        elementos.Should().Equal("A", "B", "C");
        cursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelvePaginaYCursorDelUltimo_CuandoHayMasFilasQueTake()
    {
        var (elementos, cursor) = PaginaDeResumenesAsistencia.Cortar(new[] { "A", "B", "C" }, 2, x => CursorOpaco.Codificar(x));

        elementos.Should().Equal("A", "B");
        CursorOpaco.TryDecodificar(cursor!, out var codigo).Should().BeTrue();
        codigo.Should().Be("B");
    }

    [Fact]
    public void Cortar_NoDevuelveCursor_CuandoLasFilasCabenEnLaPagina()
    {
        var (elementos, cursor) = PaginaDeResumenesAsistencia.Cortar(new[] { "C" }, 2, x => CursorOpaco.Codificar(x));

        elementos.Should().Equal("C");
        cursor.Should().BeNull();
    }
}
