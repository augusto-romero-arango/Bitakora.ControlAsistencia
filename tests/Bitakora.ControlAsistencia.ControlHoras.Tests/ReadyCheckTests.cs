using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests;

public class ReadyCheckTests
{
    private sealed class FakeEventStoreReadinessProbeExitoso : IEventStoreReadinessProbe
    {
        public Task VerificarAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeEventStoreReadinessProbeQueFalla : IEventStoreReadinessProbe
    {
        public Task VerificarAsync(CancellationToken ct) =>
            throw new TimeoutException("Npgsql: timeout abriendo la conexion");
    }

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task Ready_Retorna200_CuandoElEventStoreResponde()
    {
        var endpoint = new ReadyCheck(new FakeEventStoreReadinessProbeExitoso());

        var resultado = await endpoint.Run(FakeHttpRequest(), CancellationToken.None);

        resultado.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Ready_Retorna503ConCuerpoDiagnosticable_CuandoElEventStoreFalla()
    {
        var endpoint = new ReadyCheck(new FakeEventStoreReadinessProbeQueFalla());

        var resultado = await endpoint.Run(FakeHttpRequest(), CancellationToken.None);

        var objectResult = resultado.Should().BeOfType<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        objectResult.Value.Should().BeOfType<string>()
            .Which.Should().Contain(ReadyCheck.Mensajes.EventStoreNoDisponible);
    }
}
