using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAdvertenciasProgramacionSemanal;

public class PaginaDeAdvertenciasTests
{
    [Fact]
    public void AcotarTake_ConservaNull_CuandoNoHayTake() =>
        PaginaDeAdvertencias.AcotarTake(null).Should().BeNull();

    [Fact]
    public void AcotarTake_AcotaAUno_CuandoTakeEsCero() =>
        PaginaDeAdvertencias.AcotarTake(0).Should().Be(1);

    [Fact]
    public void AcotarTake_AcotaAUno_CuandoTakeEsNegativo() =>
        PaginaDeAdvertencias.AcotarTake(-5).Should().Be(1);

    [Fact]
    public void AcotarTake_ConservaElValor_CuandoEstaEnRango() =>
        PaginaDeAdvertencias.AcotarTake(1).Should().Be(1);

    [Fact]
    public void AcotarTake_AcotaADoscientos_CuandoTakeEsQuinientos() =>
        PaginaDeAdvertencias.AcotarTake(500).Should().Be(PaginaDeAdvertencias.TakeMaximo);

    [Fact]
    public void Cortar_DevuelveTodoSinCursor_CuandoNoHayTake()
    {
        var (elementos, cursor) = PaginaDeAdvertencias.Cortar(["A", "B"], null, c => c);

        elementos.Should().Equal("A", "B");
        cursor.Should().BeNull();
    }

    [Fact]
    public void Cortar_DevuelveCursorOpacoDelUltimo_CuandoHayMasQueTake()
    {
        var (elementos, cursor) = PaginaDeAdvertencias.Cortar(["A", "B"], 1, c => c);

        elementos.Should().Equal("A");
        CursorOpaco.TryDecodificar(cursor!, out var codigo).Should().BeTrue();
        codigo.Should().Be("A");
    }

    [Fact]
    public void Cortar_OmiteCursor_CuandoLasFilasCabenEnTake()
    {
        var (elementos, cursor) = PaginaDeAdvertencias.Cortar(["B"], 1, c => c);

        elementos.Should().Equal("B");
        cursor.Should().BeNull();
    }
}
