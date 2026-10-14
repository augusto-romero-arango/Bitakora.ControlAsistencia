using AwesomeAssertions;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.ListarFichasSede;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ListarFichasSede;

public class PaginaFichasSedeTests
{
    private static FichaSede Ficha(string codigo) =>
        new($"s:{codigo}", codigo, $"Sede {codigo}", null, null, null, true, []);

    private static readonly FichaSede A = Ficha("A01");
    private static readonly FichaSede B = Ficha("B02");
    private static readonly FichaSede C = Ficha("C03");

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var respuesta = PaginaFichasSede.Cortar([A, B, C], null);

        respuesta.Elementos.Should().Equal(A, B, C);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelveTakeElementosYCursorDelUltimo_CuandoSobranFilas()
    {
        var respuesta = PaginaFichasSede.Cortar([A, B, C], 2);

        respuesta.Elementos.Should().Equal(A, B);
        CursorFichaSede.Decodificar(respuesta.SiguienteCursor!).Should().Be(new CursorFichaSede("B02", "s:B02"));
    }

    [Fact]
    public void Cortar_NoTraeCursor_CuandoLasFilasPedidasNoExcedenElTake()
    {
        var respuesta = PaginaFichasSede.Cortar([A, B], 2);

        respuesta.Elementos.Should().Equal(A, B);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Decodificar_RetornaNull_CuandoElTextoNoEsUnCursor()
    {
        CursorFichaSede.Decodificar("%%no-es-un-cursor").Should().BeNull();
    }

    [Fact]
    public void Codificar_RecuperaLasClaves_CuandoSeDecodifica()
    {
        var cursor = new CursorFichaSede("B02", "s:B02");

        CursorFichaSede.Decodificar(cursor.Codificar()).Should().Be(cursor);
    }

    [Fact]
    public void Componer_OrdenaPorCodigoOrdinal_CuandoNoHayTake()
    {
        var minuscula = Ficha("a00");

        var respuesta = PaginaFichasSede.Componer([C, minuscula, B, A], null, null);

        respuesta.Elementos.Should().Equal(A, B, C, minuscula);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_ContinuaDespuesDelCursorSinRepetirNiSaltar_CuandoSeReenviaElCursor()
    {
        var primera = PaginaFichasSede.Componer([C, B, A], 1, null);
        var segunda = PaginaFichasSede.Componer(
            [C, B, A], 1, CursorFichaSede.Decodificar(primera.SiguienteCursor!));
        var tercera = PaginaFichasSede.Componer(
            [C, B, A], 1, CursorFichaSede.Decodificar(segunda.SiguienteCursor!));

        primera.Elementos.Should().Equal(A);
        segunda.Elementos.Should().Equal(B);
        tercera.Elementos.Should().Equal(C);
        tercera.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_RetornaListaVacia_CuandoNoHaySedes()
    {
        var respuesta = PaginaFichasSede.Componer([], 5, null);

        respuesta.Elementos.Should().BeEmpty();
        respuesta.SiguienteCursor.Should().BeNull();
    }
}
