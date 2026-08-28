// Issue #458 (MEF-ADR-0043 paso 2): tests del endpoint HTTP PUT sedes/{codigo}/centro-de-costos.
// CA-ADR-0030 / MEF-ADR-0004: KeyNotFoundException -> 404, exito -> 202.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.AsignarCentroDeCostosFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static AsignarCentroDeCostosBody BodyValido() => new("CC-100");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-1
    [Fact]
    public async Task AsignarCentroDeCostos_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<AsignarCentroDeCostosBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-5: sede inexistente -> 404
    [Fact]
    public async Task AsignarCentroDeCostos_Retorna404_CuandoSedeNoExiste()
    {
        var validator = new FakeRequestValidator<AsignarCentroDeCostosBody>(BodyValido());
        var router = new FakeCommandRouter(new KeyNotFoundException("La sede no existe"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // CA-5: CC vacio -> 400
    [Fact]
    public async Task AsignarCentroDeCostos_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<AsignarCentroDeCostosBody>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde antes de tocar el comando -- invariante URL-safe
    // ganada en #456 (MEF-ADR-0043 seccion 1.3), mismo criterio que ModificarNombreSede/
    // ActualizarUbicacionSede.
    [Fact]
    public async Task AsignarCentroDeCostos_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var validator = new FakeRequestValidator<AsignarCentroDeCostosBody>(BodyValido());
        var router = new FakeCommandRouter(
            new KeyNotFoundException("el comando nunca debe despacharse con un codigo invalido"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
