using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ListarLimitesDeJornada;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarLimitesDeJornada;

// CA-2: los guards de take y cursor retornan ANTES de abrir la QuerySession -- el IDocumentStore es
// null! a proposito, cualquier lectura del store lanzaria.
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
    public async Task ListarLimitesDeJornada_Retorna400_CuandoTakeEsMayorA200()
    {
        (await EjecutarAsync("?take=201")).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarLimitesDeJornada_Retorna400_CuandoTakeNoEsPositivo()
    {
        (await EjecutarAsync("?take=0")).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ListarLimitesDeJornada_Retorna400_CuandoElCursorEsInvalido()
    {
        (await EjecutarAsync("?cursor=%25%25no-es-un-cursor")).Should().Be(StatusCodes.Status400BadRequest);
    }
}
