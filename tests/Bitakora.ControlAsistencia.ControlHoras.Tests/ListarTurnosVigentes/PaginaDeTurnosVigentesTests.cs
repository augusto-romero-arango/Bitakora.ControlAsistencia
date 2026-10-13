using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarTurnosVigentes;

public class PaginaDeTurnosVigentesTests
{
    private static readonly (string Codigo, DateOnly Fecha)[] Turnos =
    [
        ("A", new DateOnly(2026, 10, 5)), ("A", new DateOnly(2026, 10, 6)), ("B", new DateOnly(2026, 10, 5))
    ];

    private static string CursorDe((string Codigo, DateOnly Fecha) t) => CursorOpaco.Codificar(t.Codigo, t.Fecha);

    [Fact]
    public void TakeEnRango_AceptaNullYLosLimites()
    {
        PaginaDeTurnosVigentes.TakeEnRango(null).Should().BeTrue();
        PaginaDeTurnosVigentes.TakeEnRango(1).Should().BeTrue();
        PaginaDeTurnosVigentes.TakeEnRango(200).Should().BeTrue();
    }

    [Fact]
    public void TakeEnRango_RechazaCeroNegativoYMasDe200()
    {
        PaginaDeTurnosVigentes.TakeEnRango(0).Should().BeFalse();
        PaginaDeTurnosVigentes.TakeEnRango(-1).Should().BeFalse();
        PaginaDeTurnosVigentes.TakeEnRango(201).Should().BeFalse();
    }

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var (elementos, cursor) = PaginaDeTurnosVigentes.Cortar(Turnos, null, CursorDe);

        elementos.Should().Equal(Turnos);
        cursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelvePaginaYCursorDelUltimo_CuandoHayMasFilasQueTake()
    {
        var (elementos, cursor) = PaginaDeTurnosVigentes.Cortar(Turnos, 2, CursorDe);

        elementos.Should().Equal(Turnos[0], Turnos[1]);
        CursorOpaco.TryDecodificar(cursor!, out var codigo, out var fecha).Should().BeTrue();
        codigo.Should().Be("A");
        fecha.Should().Be(new DateOnly(2026, 10, 6));
    }

    [Fact]
    public void Cortar_NoDevuelveCursor_CuandoLasFilasCabenEnLaPagina()
    {
        var (elementos, cursor) = PaginaDeTurnosVigentes.Cortar(Turnos.Take(2).ToList(), 2, CursorDe);

        elementos.Should().HaveCount(2);
        cursor.Should().BeNull();
    }
}
