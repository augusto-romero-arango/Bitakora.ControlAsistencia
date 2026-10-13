using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarFichasTurno;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarFichasTurno;

public class PaginaFichasTurnoTests
{
    private static FichaTurno Ficha(string id, string nombre) => new(id, nombre, false, "", [], "", true);

    private static readonly FichaTurno A = Ficha("id-a", "Cocina Manana");
    private static readonly FichaTurno B = Ficha("id-b", "Cocina Noche");
    private static readonly FichaTurno C = Ficha("id-c", "Recepcion");

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var respuesta = PaginaFichasTurno.Cortar([A, B, C], null);

        respuesta.Elementos.Should().Equal(A, B, C);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelveTakeElementosYCursorDelUltimo_CuandoSobranFilas()
    {
        var respuesta = PaginaFichasTurno.Cortar([A, B, C], 2);

        respuesta.Elementos.Should().Equal(A, B);
        CursorFichaTurno.Decodificar(respuesta.SiguienteCursor!).Should().Be(new CursorFichaTurno("Cocina Noche", "id-b"));
    }

    [Fact]
    public void Cortar_NoTraeCursor_CuandoLasFilasPedidasNoExcedenElTake()
    {
        var respuesta = PaginaFichasTurno.Cortar([A, B], 2);

        respuesta.Elementos.Should().Equal(A, B);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Decodificar_RetornaNull_CuandoElTextoNoEsUnCursor()
    {
        CursorFichaTurno.Decodificar("%%no-es-un-cursor").Should().BeNull();
    }

    [Fact]
    public void Codificar_RecuperaLasClaves_CuandoSeDecodifica()
    {
        var cursor = new CursorFichaTurno("Cocina Noche", "id-b");

        CursorFichaTurno.Decodificar(cursor.Codificar()).Should().Be(cursor);
    }
}
