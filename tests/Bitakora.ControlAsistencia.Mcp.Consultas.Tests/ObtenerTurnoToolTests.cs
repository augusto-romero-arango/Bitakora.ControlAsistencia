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
        json["completo"]!.GetValue<bool>().Should().BeTrue();
        json["franjas"]!.AsArray().Select(f => f!.GetValue<string>()).Should()
            .Equal("08:00-16:00, sede: [TEST] Centro");
    }

    // CA-2 (issue #612): un turno incompleto (sin franjas, CA-ADR-0033) expone completo: false tal
    // como viene de la ficha; horario y franjas viajan sin remodelar ("Sin franjas" y []).
    [Fact]
    public async Task ObtenerTurno_ExponeCompletoFalso_CuandoElTurnoNoTieneFranjas()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("obtener-turno-incompleto.json"));
        var tool = new ObtenerTurnoTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "01a05a10-1f2e-7c3a-9d0e-2f0a5a1e3b90", TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["completo"]!.GetValue<bool>().Should().BeFalse();
        json["horario"]!.GetValue<string>().Should().Be("Sin franjas");
        json["franjas"]!.AsArray().Should().BeEmpty();
    }

    // CA-3 (issue #612): la notacion compacta unifica el offset de inicio y de fin de cada franja e
    // hija (descanso/extra), reemplazando la vieja forma "(+1)" que solo mostraba el offset del fin
    // y ocultaba el de las hijas (un descanso de madrugada 02:00+1-02:30+1 se veia ambiguo como
    // 02:00-02:30(+1)). Formato replicado del eco de las tools de Comandos (#609-#611).
    [Fact]
    public async Task ObtenerTurno_MuestraOffsetDeInicioYFin_CuandoLaFranjaNocturnaTieneDescansoDeMadrugada()
    {
        var (cliente, _) = ClienteFalso.Con(
            Fixtures.Leer("obtener-turno-nocturno-con-descanso-madrugada.json"));
        var tool = new ObtenerTurnoTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "01a05a10-6b7c-7d8e-9f0a-1b2c3d4e5f60", TestContext.Current.CancellationToken);

        var franjas = JsonNode.Parse(resultado)!["franjas"]!.AsArray()
            .Select(f => f!.GetValue<string>());

        franjas.Should().Equal("22:00-06:00+1, descanso 02:00+1-02:30+1, sede: [TEST] Suba");
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
