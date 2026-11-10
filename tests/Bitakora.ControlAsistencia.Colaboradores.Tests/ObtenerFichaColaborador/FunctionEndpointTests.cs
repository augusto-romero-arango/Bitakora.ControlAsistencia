// El 200/404 depende de session.LoadAsync contra Marten real: lo cubre el smoke test.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ObtenerFichaColaborador;

public class FunctionEndpointTests
{
    // Store y tenantContext nulos a proposito: los casos deben resolverse antes de tocar Marten.
    private static FunctionEndpoint Endpoint() => new(store: null!, tenantContext: null!);

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    private static Task<IActionResult> ObtenerAsync(string id) =>
        Endpoint().Run(FakeHttpRequest(), id, CancellationToken.None);

    // El 400 lleva mensaje (MEF-ADR-0037 seccion 2): un BadRequestResult pelado deja al cliente sin
    // saber que componente de la ruta se rechazo, y sin esta asercion pasaria en verde.
    private static void DebeSer400ConMensaje(IActionResult resultado) =>
        resultado.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<string>()
            .Which.Should().NotBeNullOrWhiteSpace();

    [Fact]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var resultado = await ObtenerAsync("CC79543210");

        DebeSer400ConMensaje(resultado);
    }

    [Fact]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoElTipoDeLaIdentificacionNoEstaEnLaListaCerrada()
    {
        var resultado = await ObtenerAsync("XX-79543210");

        DebeSer400ConMensaje(resultado);
    }

    [Fact]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoElNumeroDeLaIdentificacionQuedaVacio()
    {
        var resultado = await ObtenerAsync("CC-");

        DebeSer400ConMensaje(resultado);
    }
}
