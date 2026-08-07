// Issue #329 CA-4: validacion del borde HTTP de ListarTurnosVigentes (desde/hasta obligatorios y
// con formato yyyy-MM-dd, rango invertido rechazado). Estos casos son los unicos del endpoint que
// NO dependen de Marten: cortocircuitan antes de abrir la QuerySession, asi que se prueban sobre
// el mismo FunctionEndpoint real que activa el host.
//
// Agregado en la revision: sin este archivo, CA-4 (fechas invalidas o desde > hasta -> 400) queda
// cubierto UNICAMENTE por el smoke test contra dev, que exige deploy y no corre en el CI del PR --
// exactamente el hueco que el precedente #290 ya habia cerrado para su propio endpoint. El
// carve-out de coverage de Functions GET (MEF-ADR-0035, issue #371) exime al endpoint que SOLO
// delega a LoadAsync/Query; este tiene tres ramas de validacion propias antes de tocar Marten.
//
// El IDocumentStore y el ITenantResolver se pasan nulos a proposito: si un cambio futuro moviera la
// apertura de la sesion ANTES de la validacion, estos tests se pondrian rojos -- que es exactamente
// la senal que se quiere (validar el request antes de tocar la base de datos).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarTurnosVigentes;

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
    public async Task ListarTurnosVigentes_Retorna400_CuandoFaltaElParametroDesde()
    {
        var resultado = await EjecutarEsperandoBadRequest("?hasta=2026-05-10");

        // El mensaje nombra el parametro que falla: un cliente que recibe "400" a secas no sabe
        // cual de los dos corregir.
        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("desde");
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoFaltaElParametroHasta()
    {
        var resultado = await EjecutarEsperandoBadRequest("?desde=2026-05-01");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("hasta");
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoDesdeTieneFormatoInvalido()
    {
        // dd-MM-yyyy en vez de yyyy-MM-dd, mismo caso que ObtenerTurnoVigente (#328).
        var resultado = await EjecutarEsperandoBadRequest("?desde=01-05-2026&hasta=2026-05-10");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("yyyy-MM-dd");
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoHastaTieneFormatoInvalido()
    {
        var resultado = await EjecutarEsperandoBadRequest("?desde=2026-05-01&hasta=10-05-2026");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("hasta");
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoHastaEsAnteriorADesde()
    {
        // Rango invertido: decision documentada en FunctionEndpoint.cs (400, no lista vacia), misma
        // eleccion que #290 congelo para el listado anterior. Este test la fija para que un cambio
        // de contrato sea deliberado y no un efecto colateral.
        var resultado = await EjecutarEsperandoBadRequest("?desde=2026-05-10&hasta=2026-05-05");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("anterior");
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoEmpleadoIdEsValidoPeroElRangoNoLoEs()
    {
        // CA-2 + CA-4: la presencia del filtro opcional empleadoId no relaja la validacion del
        // rango -- la consulta del Trabajador pasa por el mismo borde que la del Programador.
        var resultado = await EjecutarEsperandoBadRequest("?desde=2026-05-10&hasta=2026-05-05&empleadoId=EMP-001");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("anterior");
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoSedeIdEsValidoPeroElRangoNoLoEs()
    {
        // Issue #337 CA-3: el tercer filtro opcional (sedeId, combinable con empleadoId) tampoco
        // relaja la validacion del rango -- la consulta del jefe de sede pasa por el mismo borde.
        // Es la unica rama del filtro por sede ejercitable sin Postgres: el predicado
        // Bloques.Any(...) lo resuelve Marten contra la base real y queda cubierto por el smoke test
        // contra dev (MEF-ADR-0013), no por este archivo -- que corre con store nulo a proposito.
        var resultado = await EjecutarEsperandoBadRequest(
            "?desde=2026-05-10&hasta=2026-05-05&empleadoId=EMP-001&sedeId=SD-SUBA");

        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("anterior");
    }
}
