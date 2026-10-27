using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarCuadrosDeTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarCuadrosDeTurnos;

public class PaginaCuadrosDeTurnosTests
{
    private static CuadroDeTurnos Cuadro(string id, string nombre) =>
        new(id, nombre, 7, [], true, null, null, []);

    private static readonly CuadroDeTurnos A = Cuadro("id-a", "Ciclo A");
    private static readonly CuadroDeTurnos B = Cuadro("id-b", "Ciclo B");
    private static readonly CuadroDeTurnos C = Cuadro("id-c", "Ciclo C");

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var respuesta = PaginaCuadrosDeTurnos.Cortar([A, B, C], null);

        respuesta.Elementos.Select(e => e.Id).Should().Equal("id-a", "id-b", "id-c");
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelveTakeElementosYCursorDelUltimo_CuandoSobranFilas()
    {
        var respuesta = PaginaCuadrosDeTurnos.Cortar([A, B, C], 2);

        respuesta.Elementos.Select(e => e.Id).Should().Equal("id-a", "id-b");
        CursorCuadroDeTurnos.Decodificar(respuesta.SiguienteCursor!)
            .Should().Be(new CursorCuadroDeTurnos("Ciclo B", "id-b"));
    }

    [Fact]
    public void Cortar_NoTraeCursor_CuandoLasFilasPedidasNoExcedenElTake()
    {
        var respuesta = PaginaCuadrosDeTurnos.Cortar([A, B], 2);

        respuesta.Elementos.Should().HaveCount(2);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Decodificar_RetornaNull_CuandoElTextoNoEsUnCursor()
    {
        CursorCuadroDeTurnos.Decodificar("%%no-es-un-cursor").Should().BeNull();
    }

    [Fact]
    public void Codificar_RecuperaLasClaves_CuandoSeDecodifica()
    {
        var cursor = new CursorCuadroDeTurnos("Ciclo B", "id-b");

        CursorCuadroDeTurnos.Decodificar(cursor.Codificar()).Should().Be(cursor);
    }

    [Fact]
    public void Componer_OrdenaPorNombreOrdinalYLuegoPorId_CuandoNoHayTake()
    {
        var empate = Cuadro("id-0", "Ciclo B");

        var respuesta = PaginaCuadrosDeTurnos.Componer([C, B, empate, A], null, null);

        respuesta.Elementos.Select(e => e.Id).Should().Equal("id-a", "id-0", "id-b", "id-c");
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_ContinuaDespuesDelCursorSinRepetirNiSaltar_CuandoSeReenviaElCursor()
    {
        var primera = PaginaCuadrosDeTurnos.Componer([C, B, A], 2, null);

        var segunda = PaginaCuadrosDeTurnos.Componer(
            [C, B, A], 2, CursorCuadroDeTurnos.Decodificar(primera.SiguienteCursor!));

        primera.Elementos.Select(e => e.Id).Should().Equal("id-a", "id-b");
        segunda.Elementos.Select(e => e.Id).Should().Equal("id-c");
        segunda.SiguienteCursor.Should().BeNull();
    }
}
