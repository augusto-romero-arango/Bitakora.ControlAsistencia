using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarAusenciasColaborador;

// Orden y clave keyset: (primer dia, Id). Sin ventana aplicada: el sobre es { elementos, siguienteCursor }.
public class PaginacionAusenciasColaboradorTests
{
    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    private static AusenciaDelColaborador Ausencia(string id, int inicio, int fin) =>
        new(Guid.Parse(id), "Vacaciones", Oct(inicio), Oct(fin), [new TramoVigente(Oct(inicio), Oct(fin))]);

    private static readonly AusenciaDelColaborador Primera = Ausencia("019600b0-0000-7000-8000-000000000001", 5, 6);
    private static readonly AusenciaDelColaborador EmpateMenorId = Ausencia("019600b0-0000-7000-8000-000000000002", 12, 13);
    private static readonly AusenciaDelColaborador EmpateMayorId = Ausencia("019600b0-0000-7000-8000-000000000003", 12, 14);

    [Fact]
    public void Componer_OrdenaPorPrimerDiaYLuegoPorId_CuandoNoHayTake()
    {
        var lista = PaginaAusenciasColaborador.Componer([EmpateMayorId, Primera, EmpateMenorId], null, null);

        lista.Elementos.Should().Equal(Primera, EmpateMenorId, EmpateMayorId);
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_DevuelveTakeElementosYCursorDelUltimo_CuandoSobranFilas()
    {
        var lista = PaginaAusenciasColaborador.Componer([EmpateMayorId, Primera, EmpateMenorId], 2, null);

        lista.Elementos.Should().Equal(Primera, EmpateMenorId);
        CursorAusenciaColaborador.Decodificar(lista.SiguienteCursor!)
            .Should().Be(new CursorAusenciaColaborador(Oct(12), EmpateMenorId.Id));
    }

    [Fact]
    public void Componer_ContinuaDespuesDelCursorDesempatandoPorId_CuandoSeEnviaElCursor()
    {
        var cursor = new CursorAusenciaColaborador(Oct(12), EmpateMenorId.Id);

        var lista = PaginaAusenciasColaborador.Componer([Primera, EmpateMenorId, EmpateMayorId], 5, cursor);

        lista.Elementos.Should().Equal(EmpateMayorId);
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Componer_RespondeElementosVacios_CuandoElColaboradorNoTieneAusencias()
    {
        var lista = PaginaAusenciasColaborador.Componer([], null, null);

        lista.Elementos.Should().BeEmpty();
        lista.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public void Decodificar_RetornaNull_CuandoElTextoNoEsUnCursor()
    {
        CursorAusenciaColaborador.Decodificar("%%no-es-un-cursor").Should().BeNull();
    }

    private static async Task<int?> EjecutarAsync(string body)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        var resultado = await new FunctionEndpoint(null!, null!).Run(context.Request, "E001", CancellationToken.None);
        return resultado.Should().BeAssignableTo<IStatusCodeActionResult>().Subject.StatusCode;
    }

    [Fact]
    public async Task ListarAusenciasColaborador_Retorna422_CuandoTakeEsMayorA200()
    {
        (await EjecutarAsync("""{"desde":"2026-10-01","hasta":"2026-10-31","take":201}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAusenciasColaborador_Retorna422_CuandoTakeNoEsPositivo()
    {
        (await EjecutarAsync("""{"desde":"2026-10-01","hasta":"2026-10-31","take":0}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarAusenciasColaborador_Retorna422_CuandoElCursorEsInvalido()
    {
        (await EjecutarAsync("""{"desde":"2026-10-01","hasta":"2026-10-31","cursor":"%%no-es-un-cursor"}"""))
            .Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
