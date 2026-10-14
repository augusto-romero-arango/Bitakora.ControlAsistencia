using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAsistenciasDiarias;

public class PaginaDeAsistenciasDiariasTests
{
    private static readonly DateOnly Desde = new(2026, 10, 5);

    private static string CursorDe(FilaAsistenciaDiaria f) => CursorOpaco.Codificar(f.Fecha);

    private static IReadOnlyList<FilaAsistenciaDiaria> CincoDiasSinDatos() =>
        SintesisCalendarioAsistencia.Completar(Desde, Desde.AddDays(4), []);

    [Fact]
    public void TakeEnRango_AceptaNullYLosLimites()
    {
        PaginaDeAsistenciasDiarias.TakeEnRango(null).Should().BeTrue();
        PaginaDeAsistenciasDiarias.TakeEnRango(1).Should().BeTrue();
        PaginaDeAsistenciasDiarias.TakeEnRango(200).Should().BeTrue();
    }

    [Fact]
    public void TakeEnRango_RechazaCeroNegativoYMasDe200()
    {
        PaginaDeAsistenciasDiarias.TakeEnRango(0).Should().BeFalse();
        PaginaDeAsistenciasDiarias.TakeEnRango(-1).Should().BeFalse();
        PaginaDeAsistenciasDiarias.TakeEnRango(201).Should().BeFalse();
    }

    [Fact]
    public void PosterioresAlCursor_ConservaTodos_CuandoNoHayCursor() =>
        PaginaDeAsistenciasDiarias.PosterioresAlCursor(CincoDiasSinDatos(), null).Should().HaveCount(5);

    [Fact]
    public void PosterioresAlCursor_ExcluyeLaFechaDelCursorYLasAnteriores()
    {
        var posteriores = PaginaDeAsistenciasDiarias.PosterioresAlCursor(CincoDiasSinDatos(), Desde.AddDays(2));

        posteriores.Select(f => f.Fecha).Should().Equal(Desde.AddDays(3), Desde.AddDays(4));
    }

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var (elementos, cursor) = PaginaDeAsistenciasDiarias.Cortar(CincoDiasSinDatos(), null, CursorDe);

        elementos.Should().HaveCount(5);
        cursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelvePaginaYCursorDelUltimoDia_CuandoHayMasDiasQueTake()
    {
        var (elementos, cursor) = PaginaDeAsistenciasDiarias.Cortar(CincoDiasSinDatos(), 2, CursorDe);

        elementos.Select(f => f.Fecha).Should().Equal(Desde, Desde.AddDays(1));
        CursorOpaco.TryDecodificar(cursor!, out var fecha).Should().BeTrue();
        fecha.Should().Be(Desde.AddDays(1));
    }

    [Fact]
    public void Paginar_EntregaLaSegundaPaginaSinCursor_CuandoSeReenviaElSiguienteCursor()
    {
        var todos = CincoDiasSinDatos();
        var primera = PaginaDeAsistenciasDiarias.Cortar(todos, 3, CursorDe);
        CursorOpaco.TryDecodificar(primera.SiguienteCursor!, out var cursorFecha).Should().BeTrue();

        var segunda = PaginaDeAsistenciasDiarias.Cortar(
            PaginaDeAsistenciasDiarias.PosterioresAlCursor(todos, cursorFecha), 3, CursorDe);

        segunda.Elementos.Select(f => f.Fecha).Should().Equal(Desde.AddDays(3), Desde.AddDays(4));
        segunda.SiguienteCursor.Should().BeNull();
    }
}
