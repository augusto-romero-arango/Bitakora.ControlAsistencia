using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarBorradoresDeProgramacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarBorradoresDeProgramacion;

public class ComponerListaBorradoresTests
{
    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    private static DiaDeBorrador Dia(DateOnly fecha) =>
        new(fecha, "Turno Manana", "Turno Manana: (06:00-14:00)", [], false);

    private static BorradorDeProgramacion Borrador(string codigo, string nombre, params DateOnly[] fechas) =>
        new(codigo, codigo, $"CC-{codigo}", nombre, fechas.Select(Dia).ToList());

    private static readonly BorradorDeProgramacion Ana = Borrador("EMP-001", "Ana Ramirez", Oct(10), Oct(20));
    private static readonly BorradorDeProgramacion Luis = Borrador("EMP-002", "Luis Perez", Oct(15));
    private static readonly BorradorDeProgramacion Zoe = Borrador("EMP-003", "Zoe Diaz", Oct(25));

    private static ListaBorradoresDeProgramacion Componer(
        IEnumerable<BorradorDeProgramacion> vistas, DateOnly? desde = null, DateOnly? hasta = null,
        int? take = null, CursorBorradoresDeProgramacion? cursor = null) =>
        ListaBorradoresDeProgramacion.Componer(Oct(18), desde, hasta, vistas, take, cursor);

    [Fact]
    public void Componer_OrdenaPorNombreCompletoYCodigo()
    {
        var mismoNombre = Borrador("EMP-009", "Ana Ramirez", Oct(11));

        var lista = Componer([Zoe, Luis, mismoNombre, Ana]);

        lista.Elementos.Select(e => e.CodigoColaborador).Should().Equal("EMP-001", "EMP-009", "EMP-002", "EMP-003");
    }

    [Fact]
    public void Componer_DevuelveElBorradorCompleto_CuandoNoHayRango()
    {
        var lista = Componer([Ana]);

        var elemento = lista.Elementos.Should().ContainSingle().Subject;
        elemento.Identificacion.Should().Be("CC-EMP-001");
        elemento.NombreCompleto.Should().Be("Ana Ramirez");
        elemento.Dias.Select(d => d.Fecha).Should().Equal(Oct(10), Oct(20));
    }

    [Fact]
    public void Componer_DejaSoloLosDiasDentroDelRangoInclusivo_YOmiteAQuienNoTieneDias()
    {
        var lista = Componer([Ana, Luis, Zoe], Oct(15), Oct(20));

        lista.Elementos.Select(e => e.CodigoColaborador).Should().Equal("EMP-001", "EMP-002");
        lista.Elementos[0].Dias.Select(d => d.Fecha).Should().Equal(Oct(20));
        lista.Elementos[1].Dias.Select(d => d.Fecha).Should().Equal(Oct(15));
    }

    [Fact]
    public void Componer_AplicaSoloDesde_CuandoNoHayHasta()
    {
        var lista = Componer([Ana, Luis, Zoe], desde: Oct(21));

        lista.Elementos.Select(e => e.CodigoColaborador).Should().Equal("EMP-003");
    }

    [Fact]
    public void Componer_MarcaVencidoSoloLosDiasAnterioresALaFechaReferencia()
    {
        var anaConHoy = Borrador("EMP-001", "Ana Ramirez", Oct(17), Oct(18), Oct(19));

        var lista = Componer([anaConHoy]);

        lista.Elementos[0].Dias.Select(d => (d.Fecha, d.Vencido)).Should().Equal(
            (Oct(17), true), (Oct(18), false), (Oct(19), false));
    }

    [Fact]
    public void Componer_NoTraeCursor_CuandoNoHayTake()
    {
        var lista = Componer([Zoe, Luis, Ana]);

        lista.Elementos.Should().HaveCount(3);
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_NoTraeCursor_CuandoTodoCabeEnLaPagina()
    {
        var lista = Componer([Zoe, Luis, Ana], take: 3);

        lista.Elementos.Should().HaveCount(3);
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_PaginaPorColaboradorConSusDiasCompletos_CuandoHayTake()
    {
        var lista = Componer([Zoe, Luis, Ana], take: 1);

        lista.Elementos.Should().ContainSingle().Which.CodigoColaborador.Should().Be("EMP-001");
        lista.Elementos[0].Dias.Should().HaveCount(2);
        lista.SiguienteCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Componer_ContinuaDespuesDelCursor_YLaUltimaPaginaNoTraeCursor()
    {
        var primera = Componer([Zoe, Luis, Ana], take: 2);
        primera.Elementos.Select(e => e.CodigoColaborador).Should().Equal("EMP-001", "EMP-002");

        var cursor = CursorBorradoresDeProgramacion.Decodificar(primera.SiguienteCursor!);
        var segunda = Componer([Zoe, Luis, Ana], take: 2, cursor: cursor);

        segunda.Elementos.Select(e => e.CodigoColaborador).Should().Equal("EMP-003");
        segunda.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Decodificar_DevuelveNull_CuandoElCursorEsInvalido()
    {
        CursorBorradoresDeProgramacion.Decodificar("no-es-un-cursor").Should().BeNull();
    }

    [Fact]
    public void Codificar_YDecodificar_ConservanLaClave()
    {
        var cursor = new CursorBorradoresDeProgramacion("Ana Ramirez", "EMP-001");

        CursorBorradoresDeProgramacion.Decodificar(cursor.Codificar()).Should().Be(cursor);
    }
}
