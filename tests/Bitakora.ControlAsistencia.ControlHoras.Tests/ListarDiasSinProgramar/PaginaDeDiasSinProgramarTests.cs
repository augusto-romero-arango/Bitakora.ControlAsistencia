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

    [Fact]
    public void PosterioresAlCursor_ConservaTodos_CuandoNoHayCursor()
    {
        var colaboradores = new ColaboradorVigente[] { new("C", Desde, null), new("A", Desde, null) };

        PaginaDeDiasSinProgramar.PosterioresAlCursor(colaboradores, null)
            .Select(c => c.CodigoColaborador).Should().Equal("C", "A");
    }

    [Fact]
    public void Paginar_EntregaLaSegundaPaginaSinCursor_CuandoSeReenviaElSiguienteCursor()
    {
        var colaboradores = new ColaboradorVigente[]
        {
            new("D", Desde, null), new("B", Desde, null), new("A", Desde, null), new("C", Desde, null)
        };
        var completoB = Enumerable.Range(0, 7).Select(d => ("B", Desde.AddDays(d))).ToList();

        var primera = Pagina(colaboradores, completoB, cursor: null);
        CursorOpaco.TryDecodificar(primera.Cursor!, out var cursor).Should().BeTrue();
        var segunda = Pagina(colaboradores, completoB, cursor);

        primera.Elementos.Select(e => e.CodigoColaborador).Should().Equal("A", "C");
        segunda.Elementos.Select(e => e.CodigoColaborador).Should().Equal("D");
        segunda.Cursor.Should().BeNull();
    }

    private static readonly DateOnly Desde = new(2026, 10, 5);

    private static (IReadOnlyList<ElementoDiasSinProgramar> Elementos, string? Cursor) Pagina(
        ColaboradorVigente[] colaboradores, IEnumerable<(string, DateOnly)> programados, string? cursor)
    {
        var pedidos = PaginaDeDiasSinProgramar.PosterioresAlCursor(colaboradores, cursor);
        var calculados = CalculadorDiasSinProgramar.Calcular(Desde, Desde.AddDays(6), pedidos, programados);
        return PaginaDeDiasSinProgramar.Cortar(calculados, take: 2, e => e.CodigoColaborador);
    }
}
