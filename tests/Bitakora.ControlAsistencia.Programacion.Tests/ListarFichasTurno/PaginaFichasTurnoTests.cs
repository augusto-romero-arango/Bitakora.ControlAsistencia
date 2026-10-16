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

    private static FranjaFicha Franja(string? sedeId) =>
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], sedeId, null, "");

    private static FichaTurno FichaConSedes(string id, string nombre, params string?[] sedes) =>
        new(id, nombre, false, "", sedes.Select(Franja).ToList(), "", true);

    private static readonly FichaTurno T1 = FichaConSedes("id-1", "Turno 1", "sede-a");
    private static readonly FichaTurno T2 = FichaConSedes("id-2", "Turno 2", "sede-b", "sede-a");
    private static readonly FichaTurno T3 = FichaConSedes("id-3", "Turno 3", "sede-b");
    private static readonly FichaTurno T4 = FichaConSedes("id-4", "Turno 4", (string?)null);

    [Fact]
    public void Componer_DevuelveSoloTurnosConAlgunaFranjaEnLaSede_CuandoSeFiltraPorSedeId()
    {
        var respuesta = PaginaFichasTurno.Componer([T4, T3, T2, T1], null, null, "sede-a");

        respuesta.Elementos.Should().Equal(T1, T2);
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_PaginaSinPerderNiRepetir_CuandoSedeIdSeCombinaConTakeYCursor()
    {
        var t5 = FichaConSedes("id-5", "Turno 5", "sede-a");
        var catalogo = new[] { t5, T4, T3, T2, T1 };

        var primera = PaginaFichasTurno.Componer(catalogo, 2, null, "sede-a");
        primera.Elementos.Should().Equal(T1, T2);
        primera.SiguienteCursor.Should().NotBeNull();

        var segunda = PaginaFichasTurno.Componer(
            catalogo, 2, CursorFichaTurno.Decodificar(primera.SiguienteCursor!), "sede-a");
        segunda.Elementos.Should().Equal(t5);
        segunda.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_DevuelveListaVacia_CuandoNingunTurnoCoincideConLaSede()
    {
        var respuesta = PaginaFichasTurno.Componer([T1, T2, T3, T4], null, null, "sede-z");

        respuesta.Elementos.Should().BeEmpty();
        respuesta.SiguienteCursor.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Componer_IgnoraElFiltro_CuandoSedeIdEsNuloOVacio(string? sedeId)
    {
        var respuesta = PaginaFichasTurno.Componer([T4, T3, T2, T1], null, null, sedeId);

        respuesta.Elementos.Should().Equal(T1, T2, T3, T4);
    }
}
