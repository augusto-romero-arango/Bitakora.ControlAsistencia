// Una fecha invalida responde 400 con un mensaje que indica el formato esperado (yyyy-MM-dd).
// El 404 y el filtro por tenant dependen de Marten real: los cubre ObtenerDepuracionDelDiaSmokeTests;
// la resolucion de IDocumentStore/ITenantContext por constructor, ComposicionServiciosTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ObtenerDepuracionDelDia;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ObtenerDepuracionDelDia;

public class FunctionEndpointTests
{
    // store/tenantContext nulos a proposito: el 400 debe resolverse antes de tocar Marten.
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantContext: null!);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static Task<IActionResult> ObtenerAsync(string codigoColaborador, string fecha) =>
        Endpoint().Run(FakeHttpRequest(), codigoColaborador, fecha, CancellationToken.None);

    // El 400 debe llevar mensaje: se afirma el tipo Y que el cuerpo trae texto (MEF-ADR-0037
    // seccion 2, proscribe el BadRequestResult pelado).
    [Fact]
    public async Task ObtenerDepuracionDelDia_Retorna400ConMensaje_CuandoLaFechaNoTieneElFormatoEsperado()
    {
        var resultado = await ObtenerAsync("EMP-001", "24-08-2026");

        resultado.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<string>()
            .Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ObtenerDepuracionDelDia_Retorna400ConMensaje_CuandoLaFechaEsTextoArbitrario()
    {
        var resultado = await ObtenerAsync("EMP-001", "no-es-una-fecha");

        resultado.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<string>()
            .Which.Should().NotBeNullOrWhiteSpace();
    }
}
