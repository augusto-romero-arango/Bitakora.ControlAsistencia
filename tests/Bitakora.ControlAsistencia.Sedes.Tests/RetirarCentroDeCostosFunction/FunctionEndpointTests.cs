// Issue #458 (MEF-ADR-0043 paso 3): tests del endpoint HTTP DELETE sedes/{codigo}/centro-de-costos.
// CA-ADR-0030 / MEF-ADR-0004: InvalidOperationException -> 409, KeyNotFoundException -> 404, exito
// -> 202.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RetirarCentroDeCostosFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-3
    [Fact]
    public async Task RetirarCentroDeCostos_Retorna202_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-4: sin CC vigente -> 409 Conflict
    [Fact]
    public async Task RetirarCentroDeCostos_Retorna409_CuandoLaSedeNoTieneCentroVigente()
    {
        var router = new FakeCommandRouter(
            new InvalidOperationException("La sede no tiene un centro de costos vigente"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-5: sede inexistente -> 404
    [Fact]
    public async Task RetirarCentroDeCostos_Retorna404_CuandoSedeNoExiste()
    {
        var router = new FakeCommandRouter(new KeyNotFoundException("La sede no existe"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde antes de tocar el comando -- invariante URL-safe
    // ganada en #456 (MEF-ADR-0043 seccion 1.3), mismo criterio que ModificarNombreSede/
    // ActualizarUbicacionSede.
    [Fact]
    public async Task RetirarCentroDeCostos_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var router = new FakeCommandRouter(
            new KeyNotFoundException("el comando nunca debe despacharse con un codigo invalido"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
