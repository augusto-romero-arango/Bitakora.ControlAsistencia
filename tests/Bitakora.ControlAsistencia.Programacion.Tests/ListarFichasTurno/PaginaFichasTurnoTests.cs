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

    [Fact]
    public void Componer_OrdenaPorNombreOrdinalYLuegoPorId_CuandoNoHayTake()
    {
        var empate = Ficha("id-0", "Cocina Noche");
        var minuscula = Ficha("id-d", "cocina");

        var respuesta = PaginaFichasTurno.Componer([C, minuscula, B, A, empate], null, null);

        respuesta.Elementos.Should().Equal(A, empate, B, C, minuscula);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_ContinuaDespuesDelCursorSinRepetirNiSaltar_CuandoSeReenviaElCursor()
    {
        var primera = PaginaFichasTurno.Componer([C, B, A], 1, null);

        var segunda = PaginaFichasTurno.Componer(
            [C, B, A], 1, CursorFichaTurno.Decodificar(primera.SiguienteCursor!));
        var tercera = PaginaFichasTurno.Componer(
            [C, B, A], 1, CursorFichaTurno.Decodificar(segunda.SiguienteCursor!));

        primera.Elementos.Should().Equal(A);
        segunda.Elementos.Should().Equal(B);
        tercera.Elementos.Should().Equal(C);
        tercera.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_DesempataPorId_CuandoElCursorApuntaAUnNombreRepetido()
    {
        var empate = Ficha("id-z", "Cocina Noche");

        var respuesta = PaginaFichasTurno.Componer([A, B, empate, C], null, new CursorFichaTurno("Cocina Noche", "id-b"));

        respuesta.Elementos.Should().Equal(empate, C);
    }
}
