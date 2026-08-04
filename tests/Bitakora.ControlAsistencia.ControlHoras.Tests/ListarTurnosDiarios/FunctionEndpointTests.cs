// Issue #290 CA-2: validacion del borde HTTP de ListarTurnosDiarios (desde/hasta obligatorios y
// con formato yyyy-MM-dd, rango invertido rechazado). Hasta este archivo, CA-2 solo estaba cubierto
// por el smoke test contra dev -- que exige deploy y no corre en el CI del PR. Estos casos son los
// unicos del endpoint que NO dependen de Marten: cortocircuitan antes de abrir la QuerySession, asi
// que se prueban sobre el mismo FunctionEndpoint real que activa el host (precedente:
// RegistrarMarcacionFunction/FunctionEndpointTests, mismo proyecto).
//
// El IDocumentStore y el ITenantResolver se pasan nulos a proposito: si un cambio futuro moviera la
// apertura de la sesion ANTES de la validacion, estos tests se pondrian rojos -- que es exactamente
// la senal que se quiere (validar el request antes de tocar la base de datos).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarTurnosDiarios;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarTurnosDiarios;

public class FunctionEndpointTests
{
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantResolver: null!);

    private static HttpRequest FakeHttpRequest(string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(queryString);
        return context.Request;
    }

    private static async Task<BadRequestObjectResult> EjecutarEsperandoBadRequest(string queryString)
    {
        var resultado = await Endpoint().Run(FakeHttpRequest(queryString), CancellationToken.None);

        return resultado.Should().BeOfType<BadRequestObjectResult>().Subject;
    }

    [Fact]
    public async Task ListarTurnosDiarios_Retorna400_CuandoFaltaElParametroDesde()
    {
        var resultado = await EjecutarEsperandoBadRequest("?hasta=2026-06-10");

        // El mensaje nombra el parametro que falta: un cliente que recibe "400" a secas no sabe
        // cual de los dos corregir.
        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("desde");
    }

    [Fact]
    public async Task ListarTurnosDiarios_Retorna400_CuandoFaltaElParametroHasta()
    {
        var resultado = await EjecutarEsperandoBadRequest("?desde=2026-06-01");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("hasta");
    }

    [Fact]
    public async Task ListarTurnosDiarios_Retorna400_CuandoDesdeTieneFormatoInvalido()
    {
        // dd-MM-yyyy en vez de yyyy-MM-dd, mismo caso que ObtenerTurnoDiario (#289).
        var resultado = await EjecutarEsperandoBadRequest("?desde=01-06-2026&hasta=2026-06-10");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("yyyy-MM-dd");
    }

    [Fact]
    public async Task ListarTurnosDiarios_Retorna400_CuandoHastaTieneFormatoInvalido()
    {
        var resultado = await EjecutarEsperandoBadRequest("?desde=2026-06-01&hasta=10-06-2026");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("hasta");
    }

    [Fact]
    public async Task ListarTurnosDiarios_Retorna400_CuandoHastaEsAnteriorADesde()
    {
        // Rango invertido: decision documentada en FunctionEndpoint.cs (400, no lista vacia). El
        // issue #290 lo dejo como "propuesta revisable" sin CA; este test congela la eleccion para
        // que un cambio de contrato sea deliberado y no un efecto colateral.
        var resultado = await EjecutarEsperandoBadRequest("?desde=2026-06-10&hasta=2026-06-05");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("anterior");
    }
}
