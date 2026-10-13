using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ListarFichasSede;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ListarFichasSede;

// Los guards de entrada retornan ANTES de abrir la QuerySession: el IDocumentStore es null! a proposito.
public class FunctionEndpointTests
{
    private static async Task<int?> EjecutarAsync(string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(queryString);
        var resultado = await new FunctionEndpoint(null!, null!).Run(context.Request, CancellationToken.None);
        return resultado.Should().BeAssignableTo<IStatusCodeActionResult>().Subject.StatusCode;
    }

    [Fact]
    public async Task ListarFichasSede_Retorna400_CuandoTakeEsMayorA200()
    {
        (await EjecutarAsync("?take=201")).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarFichasSede_Retorna400_CuandoTakeNoEsPositivo()
    {
        (await EjecutarAsync("?take=0")).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarFichasSede_Retorna400_CuandoTakeNoEsNumerico()
    {
        (await EjecutarAsync("?take=abc")).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarFichasSede_Retorna400_CuandoElCursorEsInvalido()
    {
        (await EjecutarAsync("?cursor=%25%25no-es-un-cursor")).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarFichasSede_Retorna400_CuandoActivaNoEsBooleano()
    {
        (await EjecutarAsync("?activa=no-es-booleano")).Should().Be(StatusCodes.Status400BadRequest);
    }
}
