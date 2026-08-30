using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerTurno;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class ObtenerTurnoToolTests
{
    [Fact]
    public async Task ObtenerTurno_RemodelaLasFranjasCompactas_CuandoElTurnoExiste()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("obtener-turno.json"));
        var tool = new ObtenerTurnoTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "019fdaab-92b6-7b57-8408-cef0e48e1a46", TestContext.Current.CancellationToken);

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should()
            .Be("/api/programacion/turnos/019fdaab-92b6-7b57-8408-cef0e48e1a46");

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["id"]!.GetValue<string>().Should().Be("019fdaab-92b6-7b57-8408-cef0e48e1a46");
        json["nombre"]!.GetValue<string>().Should().Be("[TEST] Turno Con Sede Prearmada");
        json["horario"]!.GetValue<string>().Should().Be("08:00-16:00");
        json["esDescanso"]!.GetValue<bool>().Should().BeFalse();
        json["franjas"]!.AsArray().Select(f => f!.GetValue<string>()).Should()
            .Equal("08:00-16:00, sede: [TEST] Centro");
    }

    [Fact]
    public async Task ObtenerTurno_CompactaDescansosYExtras_CuandoLaFranjaLosTiene()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("obtener-turno-con-descansos-variante.json"));
        var tool = new ObtenerTurnoTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "019fdaab-5eca-719a-9125-7541f0f07087", TestContext.Current.CancellationToken);

        var franjas = JsonNode.Parse(resultado)!["franjas"]!.AsArray()
            .Select(f => f!.GetValue<string>());

        franjas.Should().Equal(
            "06:00-10:00, descanso 12:00-13:00, extra 16:00-18:00, sede: [TEST] Suba",
            "14:00-18:00");
    }

    [Fact]
    public async Task ObtenerTurno_RespondeMensajeNoExiste_CuandoElDominioDevuelve404()
    {
        var (cliente, _) = ClienteFalso.Con("", HttpStatusCode.NotFound);
        var tool = new ObtenerTurnoTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, "id-inexistente", TestContext.Current.CancellationToken);

        resultado.Should().Be("No existe un turno con id 'id-inexistente' en el catalogo.");
    }
}
