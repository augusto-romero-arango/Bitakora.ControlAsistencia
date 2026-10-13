using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarCuadrosSemanalesTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarCuadrosSemanalesTurnos;

public class PaginaCuadrosSemanalesTurnosTests
{
    private static CuadroSemanalTurnos Cuadro(string id, string nombre) =>
        new(id, nombre, 1, [], true, null, null, []);

    private static readonly CuadroSemanalTurnos A = Cuadro("id-a", "Semana A");
    private static readonly CuadroSemanalTurnos B = Cuadro("id-b", "Semana B");
    private static readonly CuadroSemanalTurnos C = Cuadro("id-c", "Semana C");

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var respuesta = PaginaCuadrosSemanalesTurnos.Cortar([A, B, C], null);

        respuesta.Elementos.Select(e => e.Id).Should().Equal("id-a", "id-b", "id-c");
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelveTakeElementosYCursorDelUltimo_CuandoSobranFilas()
    {
        var respuesta = PaginaCuadrosSemanalesTurnos.Cortar([A, B, C], 2);

        respuesta.Elementos.Select(e => e.Id).Should().Equal("id-a", "id-b");
        CursorCuadroSemanalTurnos.Decodificar(respuesta.SiguienteCursor!)
            .Should().Be(new CursorCuadroSemanalTurnos("Semana B", "id-b"));
    }

    [Fact]
    public void Cortar_NoTraeCursor_CuandoLasFilasPedidasNoExcedenElTake()
    {
        var respuesta = PaginaCuadrosSemanalesTurnos.Cortar([A, B], 2);

        respuesta.Elementos.Should().HaveCount(2);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Decodificar_RetornaNull_CuandoElTextoNoEsUnCursor()
    {
        CursorCuadroSemanalTurnos.Decodificar("%%no-es-un-cursor").Should().BeNull();
    }

    [Fact]
    public void Codificar_RecuperaLasClaves_CuandoSeDecodifica()
    {
        var cursor = new CursorCuadroSemanalTurnos("Semana B", "id-b");

        CursorCuadroSemanalTurnos.Decodificar(cursor.Codificar()).Should().Be(cursor);
    }
}
