// Paso 1 de la paginacion keyset sobre la lista de codigos que el cliente trae (CA-4). El resto del
// keyset -- la rama que descubre el universo con un distinct sobre Marten -- solo lo cubre el smoke
// test: exige Postgres real.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarResumenesAsistencia;

public class PaginaDeCodigosTests
{
    [Fact]
    public void AcotarTake_DevuelveElTakePedido_CuandoEstaDentroDeLaCota()
    {
        PaginaDeCodigos.AcotarTake(50).Should().Be(50);
    }

    [Fact]
    public void AcotarTake_RecortaAlTopeDelServidor_CuandoElClientePideMasDe200()
    {
        PaginaDeCodigos.AcotarTake(5000).Should().Be(200);
    }

    [Fact]
    public void AcotarTake_DevuelveUno_CuandoElClientePideCeroONegativo()
    {
        PaginaDeCodigos.AcotarTake(0).Should().Be(1);
        PaginaDeCodigos.AcotarTake(-10).Should().Be(1);
    }

    [Fact]
    public void Recortar_OrdenaAscendenteYCortaEnTake_CuandoNoHayCursor()
    {
        var pagina = PaginaDeCodigos.Recortar(["EMP-C", "EMP-A", "EMP-B"], cursor: null, take: 2);

        pagina.Should().Equal("EMP-A", "EMP-B");
    }

    [Fact]
    public void Recortar_DevuelveSoloLosCodigosPosterioresAlCursor_CuandoSeEnviaCursor()
    {
        var pagina = PaginaDeCodigos.Recortar(["EMP-A", "EMP-B", "EMP-C"], "EMP-A", take: 50);

        pagina.Should().Equal("EMP-B", "EMP-C");
    }

    // El cursor es el codigo de la ULTIMA fila recibida: esa fila no puede repetirse en la pagina
    // siguiente.
    [Fact]
    public void Recortar_ExcluyeElCodigoDelCursor_CuandoElCursorCoincideExactamenteConUnCodigo()
    {
        var pagina = PaginaDeCodigos.Recortar(["EMP-A", "EMP-B"], "EMP-B", take: 50);

        pagina.Should().BeEmpty();
    }

    [Fact]
    public void Recortar_DevuelveMenosFilasQueTake_CuandoEsLaUltimaPagina()
    {
        var pagina = PaginaDeCodigos.Recortar(["EMP-A", "EMP-B", "EMP-C"], "EMP-B", take: 2);

        pagina.Should().Equal("EMP-C");
    }

    [Fact]
    public void Recortar_ColapsaLosCodigosRepetidos_CuandoElClienteEnviaElMismoCodigoDosVeces()
    {
        var pagina = PaginaDeCodigos.Recortar(["EMP-A", "EMP-A", "EMP-B"], cursor: null, take: 50);

        pagina.Should().Equal("EMP-A", "EMP-B");
    }

    // El Take crudo del cliente nunca alcanza la pagina, ni por esta via ni por el endpoint.
    [Fact]
    public void Recortar_AcotaElTakeAlTopeDelServidor_CuandoElClientePideMasDe200()
    {
        var codigos = Enumerable.Range(1, 300).Select(n => $"EMP-{n:D4}").ToList();

        var pagina = PaginaDeCodigos.Recortar(codigos, cursor: null, take: 5000);

        pagina.Should().HaveCount(200);
    }

    [Fact]
    public void Recortar_NoDevuelveNingunCodigo_CuandoLaListaPedidaEstaVacia()
    {
        var pagina = PaginaDeCodigos.Recortar([], cursor: null, take: 50);

        pagina.Should().BeEmpty();
    }
}
