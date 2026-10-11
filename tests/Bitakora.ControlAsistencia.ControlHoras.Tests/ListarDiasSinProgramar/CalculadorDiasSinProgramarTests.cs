using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarDiasSinProgramar;

public class CalculadorDiasSinProgramarTests
{
    private static readonly DateOnly Lunes = new(2026, 10, 5);
    private static readonly DateOnly Domingo = new(2026, 10, 11);

    private static DateOnly Dia(int d) => new(2026, 10, d);

    private static IEnumerable<(string, DateOnly)> Programados(string codigo, params int[] dias) =>
        dias.Select(d => (codigo, Dia(d)));

    private static IReadOnlyList<ElementoDiasSinProgramar> Calcular(
        ColaboradorVigente[] colaboradores, IEnumerable<(string, DateOnly)> programados) =>
        CalculadorDiasSinProgramar.Calcular(Lunes, Domingo, colaboradores, programados);

    [Fact]
    public void Calcular_ListaLosDiasSinDocumento_CuandoElColaboradorTieneAlgunosProgramados()
    {
        var resultado = Calcular(
            [new("A", Dia(1), null)],
            Programados("A", 5, 6, 7));

        var elemento = resultado.Should().ContainSingle().Subject;
        elemento.CodigoColaborador.Should().Be("A");
        elemento.DiasSinProgramar.Should().Equal(Dia(8), Dia(9), Dia(10), Dia(11));
    }

    [Fact]
    public void Calcular_OmiteAlColaborador_CuandoTieneLosSieteDiasProgramados()
    {
        var resultado = Calcular(
            [new("B", Dia(1), null)],
            Programados("B", 5, 6, 7, 8, 9, 10, 11));

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Calcular_ListaLosSieteDias_CuandoElColaboradorNoTieneDocumentos()
    {
        var resultado = Calcular([new("C", Dia(1), null)], []);

        resultado.Single().DiasSinProgramar.Should().Equal(
            Dia(5), Dia(6), Dia(7), Dia(8), Dia(9), Dia(10), Dia(11));
    }

    [Fact]
    public void Calcular_ExcluyeDiasAnterioresALaVigencia_CuandoVigenteDesdeEstaDentroDeLaVentana()
    {
        var resultado = Calcular([new("D", Dia(9), null)], []);

        resultado.Single().DiasSinProgramar.Should().Equal(Dia(9), Dia(10), Dia(11));
    }

    [Fact]
    public void Calcular_ExcluyeDiasPosterioresALaVigencia_CuandoVigenteHastaEstaDentroDeLaVentana()
    {
        var resultado = Calcular([new("E", Dia(1), Dia(6))], []);

        resultado.Single().DiasSinProgramar.Should().Equal(Dia(5), Dia(6));
    }

    [Fact]
    public void Calcular_OmiteAlColaborador_CuandoSuVigenciaNoTocaLaVentana()
    {
        var resultado = Calcular(
            [new("F", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)),
             new("G", new DateOnly(2026, 11, 1), null)],
            []);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Calcular_IgnoraDocumentosDeOtraFechaOColaboradorNoPedido()
    {
        var programados = Programados("A", 5)
            .Append(("A", new DateOnly(2026, 10, 20)))
            .Concat(Programados("Z", 6, 7, 8, 9, 10, 11));

        var resultado = Calcular([new("A", Dia(1), null)], programados);

        resultado.Single().DiasSinProgramar.Should().Equal(Dia(6), Dia(7), Dia(8), Dia(9), Dia(10), Dia(11));
    }

    [Fact]
    public void Calcular_OrdenaPorCodigoAscendente_CuandoLosColaboradoresVienenDesordenados()
    {
        var resultado = Calcular(
            [new("D", Dia(1), null), new("A", Dia(1), null), new("C", Dia(1), null)],
            []);

        resultado.Select(e => e.CodigoColaborador).Should().Equal("A", "C", "D");
    }
}
