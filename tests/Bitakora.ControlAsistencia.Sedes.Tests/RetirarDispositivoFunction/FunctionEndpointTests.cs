using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RetirarDispositivoFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";
    private const string DispositivoId = "DISP-100";

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-3
    [Fact]
    public async Task RetirarDispositivo_Retorna202_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, DispositivoId, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-4: dispositivo no instalado en esta sede -> 404
    [Fact]
    public async Task RetirarDispositivo_Retorna404_CuandoElDispositivoNoEstaInstalado()
    {
        var router = new FakeCommandRouter(
            new KeyNotFoundException("El dispositivo no esta instalado en esta sede"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, DispositivoId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // Sede inexistente -> 404 (precondicion de orquestacion, no un CA propio del issue)
    [Fact]
    public async Task RetirarDispositivo_Retorna404_CuandoSedeNoExiste()
    {
        var router = new FakeCommandRouter(new KeyNotFoundException("La sede no existe"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, DispositivoId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde: el FakeCommandRouter lanzaria si el comando
    // llegara a despacharse con un codigo invalido.
    [Fact]
    public async Task RetirarDispositivo_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var router = new FakeCommandRouter(
            new KeyNotFoundException("el comando nunca debe despacharse con un codigo invalido"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", DispositivoId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
