// Issue #440: migracion de ListarTurnosVigentes de GET a QUERY (MEF-ADR-0042). Guards del borde
// HTTP (415/400/422), la unica capa de CA-3 que corre en CI -- el smoke test que lo cubre
// end-to-end depende del deploy (CA-6) y el test de composicion de ComposicionServiciosTests solo
// verifica wiring (resolucion de IDocumentStore/ITenantResolver por constructor).
//
// Reemplaza la matriz anterior de 7 casos de 400 sobre query string (issue #329/#337): el filtro
// ahora viaja como DTO tipado en el body JSON (FiltroListarTurnosVigentes), asi que "ausente" y
// "malformado" dejan de compartir un unico 400 -- se separan en 400 (JSON invalido) y 422
// (DesdeFecha/HastaFecha ausentes o rango invertido), MEF-ADR-0042 seccion 3.
//
// A diferencia del precedente ListarAsistenciasDiarias (#427), CodigoColaborador y SedeId son
// OPCIONALES aqui (issue #440, "Diferencia con el precedente que el implementer debe respetar"):
// no hay caso 422 por su ausencia.
//
// El IDocumentStore se pasa null! a proposito: los tres guards deben retornar ANTES de abrir la
// QuerySession, asi que mover la apertura de sesion por encima de ellos rompe estos tests con
// NullReferenceException en vez de pasar inadvertido.
//
// Reforzado con aserciones de CONTENIDO del mensaje (no solo StatusCode) en los casos 400: contra
// el FunctionEndpoint.cs todavia-GET de esta fase roja, cualquier body sin querystring "desde"
// cae en el mismo BadRequestObjectResult(400) que el guard legado -- coincide el StatusCode con
// el esperado por pura casualidad de numero, pero el mensaje NO menciona JSON/body/query. Sin este
// refuerzo esos dos tests pasarian en falso contra el codigo legado.

using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarTurnosVigentes;

public class FunctionEndpointTests
{
    private static FunctionEndpoint Endpoint() => new(null!, new TenantResolverFijo());

    private static HttpRequest Request(string? contentType, string body)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.ContentType = contentType;
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        return context.Request;
    }

    private static HttpRequest RequestJson(string body) => Request("application/json", body);

    private static async Task<ObjectResult> EjecutarAsync(HttpRequest request)
    {
        var resultado = await Endpoint().Run(request, CancellationToken.None);
        return resultado.Should().BeAssignableTo<ObjectResult>().Subject;
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna415_CuandoElContentTypeNoEsJson()
    {
        var resultado = await EjecutarAsync(Request("text/plain", "{}"));

        resultado.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna415_CuandoNoViajaContentType()
    {
        var resultado = await EjecutarAsync(Request(contentType: null, body: "{}"));

        resultado.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var resultado = await EjecutarAsync(RequestJson("{ esto no es json valido"));

        resultado.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("JSON");
    }

    // El literal null es JSON valido y deserializa a null: rama distinta del catch de
    // JsonException.
    [Fact]
    public async Task ListarTurnosVigentes_Retorna400_CuandoElBodyDeserializaANull()
    {
        var resultado = await EjecutarAsync(RequestJson("null"));

        resultado.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        resultado.Value.Should().BeOfType<string>().Which.Should().Contain("query");
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoDesdeFechaEstaAusente()
    {
        var resultado = await EjecutarAsync(RequestJson("""{"hastaFecha":"2026-05-10"}"""));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoHastaFechaEstaAusente()
    {
        var resultado = await EjecutarAsync(RequestJson("""{"desdeFecha":"2026-05-01"}"""));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    // CA-2: CodigoColaborador y SedeId son opcionales -- su ausencia (aqui, ambos ademas de las
    // fechas) no debe producir un 422 propio distinto al de las fechas ausentes.
    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoElFiltroLlegaVacio()
    {
        var resultado = await EjecutarAsync(RequestJson("{}"));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoElRangoEstaInvertido()
    {
        var resultado = await EjecutarAsync(RequestJson(
            """{"desdeFecha":"2026-05-10","hastaFecha":"2026-05-05"}"""));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    // CA-2 + CA-3 (issue #337): la presencia de los dos filtros opcionales no relaja la validacion
    // del rango -- la consulta del Trabajador filtrada por sede pasa por el mismo borde que el
    // panorama del Programador.
    [Fact]
    public async Task ListarTurnosVigentes_Retorna422_CuandoCodigoColaboradorYSedeIdSonValidosPeroElRangoNoLoEs()
    {
        var resultado = await EjecutarAsync(RequestJson(
            """
            {"desdeFecha":"2026-05-10","hastaFecha":"2026-05-05","codigoColaborador":"EMP-001","sedeId":"SD-SUBA"}
            """));

        resultado.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
