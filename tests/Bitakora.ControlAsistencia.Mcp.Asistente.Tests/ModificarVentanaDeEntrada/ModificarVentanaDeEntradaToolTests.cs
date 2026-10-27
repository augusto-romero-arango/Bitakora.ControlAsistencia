using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ModificarVentanaDeEntrada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ModificarVentanaDeEntrada;

public class ModificarVentanaDeEntradaToolTests
{
    private const string TurnoId = "8f14e45f-ceea-4b3c-8f0a-000000000020";
    private const string TurnoSinFranjasId = "8f14e45f-ceea-4b3c-8f0a-000000000021";
    private const string TurnoInexistenteId = "8f14e45f-ceea-4b3c-8f0a-0000000000ff";

    private static string Ruta(string id) => $"/api/programacion/turnos/{id}/ventana-entrada";

    private static (ModificarVentanaDeEntradaTool Tool, HandlerPorRuta Handler) CrearTool(
        HttpStatusCode statusPut = HttpStatusCode.NoContent, string cuerpoPut = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        var catalogo = Fixtures.Leer("ModificarVentanaDeEntrada", "turnos.json");
        handler.RespondeDetalleDeTurno(catalogo, TurnoId);
        handler.RespondeDetalleDeTurno(catalogo, TurnoSinFranjasId);
        handler.RespondeDetalleDeTurno(catalogo, TurnoInexistenteId);
        handler.Responde(HttpMethod.Put, Ruta(TurnoId), statusPut, cuerpoPut);
        handler.Responde(HttpMethod.Put, Ruta(TurnoSinFranjasId), statusPut, cuerpoPut);
        var api = new ProgramacionApi(cliente);
        return (new ModificarVentanaDeEntradaTool(api, new ResolutorTurnoPorId(api)), handler);
    }

    private static Task<string> Ejecutar(ModificarVentanaDeEntradaTool tool, string turnoId, string amplitud) =>
        tool.Run(null!, turnoId, amplitud, TestContext.Current.CancellationToken);

    [Fact]
    public async Task ModificarVentanaDeEntrada_EnviaElPutYRespondeConElEco_CuandoLaAmplitudEsTresHoras()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, TurnoId, "3:00");

        var put = handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        put.Ruta.Should().Be(Ruta(TurnoId));
        var amplitud = JsonNode.Parse(put.Cuerpo!)!["amplitud"]!;
        amplitud["horas"]!.GetValue<int>().Should().Be(3);
        amplitud["minutos"]!.GetValue<int>().Should().Be(0);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["resultado"]!.GetValue<string>().Should().Be(ModificarVentanaDeEntradaTool.Mensajes.ResultadoVentanaModificada);
        json["turno"]!.GetValue<string>().Should().Be("Oficina");
        json["ventanaDeEntrada"]!.GetValue<string>().Should().Be("entra entre 07:00 y 10:00 (3 h)");
        json["nota"]!.GetValue<string>().Should().Be(ModificarVentanaDeEntradaTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_IncluyeLosMinutosEnElEco_CuandoLaAmplitudTieneMinutos()
    {
        var (tool, _) = CrearTool();

        var resultado = await Ejecutar(tool, TurnoId, "2:30");

        JsonNode.Parse(resultado)!["ventanaDeEntrada"]!.GetValue<string>()
            .Should().Be("entra entre 07:00 y 09:30 (2 h 30 min)");
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_DiceSoloMinutosEnElEco_CuandoLaAmplitudNoTieneHoras()
    {
        var (tool, _) = CrearTool();

        var resultado = await Ejecutar(tool, TurnoId, "0:45");

        JsonNode.Parse(resultado)!["ventanaDeEntrada"]!.GetValue<string>()
            .Should().Be("entra entre 07:00 y 07:45 (45 min)");
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_DiceTurnoFijo_CuandoLaAmplitudEsCero()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, TurnoId, "0:00");

        handler.Requests.Should().ContainSingle(r => r.Metodo == HttpMethod.Put);
        JsonNode.Parse(resultado)!["ventanaDeEntrada"]!.GetValue<string>().Should().Be("turno fijo");
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_DiceSoloLaAmplitud_CuandoLaFichaLeidaNoTieneFranjas()
    {
        var (tool, _) = CrearTool();

        var resultado = await Ejecutar(tool, TurnoSinFranjasId, "3:00");

        JsonNode.Parse(resultado)!["ventanaDeEntrada"]!.GetValue<string>().Should().Be("ventana de 3 h");
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_RechazaSinLlamar_CuandoTurnoIdEstaEnBlanco()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, "   ", "3:00");

        resultado.Should().Be(string.Format(ModificarVentanaDeEntradaTool.Mensajes.TurnoIdInvalido, "   "));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_RespondeAmplitudInvalidaSinLlamar_CuandoNoEsHmm()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, TurnoId, "3h");

        resultado.Should().Be(string.Format(ModificarVentanaDeEntradaTool.Mensajes.AmplitudInvalida, "3h"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_RespondeTurnoNoExisteSinPut_Cuando404EnElDetalle()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, TurnoInexistenteId, "3:00");

        resultado.Should().Be(string.Format(ModificarVentanaDeEntradaTool.Mensajes.TurnoNoExiste, TurnoInexistenteId));
        resultado.Should().Contain("buscar_turnos").And.NotContain("disponibles");
        handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_RechazaSinLlamar_CuandoTurnoIdNoEsUnGuid()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, "Oficina", "3:00");

        resultado.Should().Be(string.Format(ModificarVentanaDeEntradaTool.Mensajes.TurnoIdInvalido, "Oficina"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LeeElDetalleDelTurnoPorId_SinConsultarElCatalogo()
    {
        var (tool, handler) = CrearTool();

        await Ejecutar(tool, TurnoId, "3:00");

        handler.Requests.Should().Contain(r =>
            r.Metodo == HttpMethod.Get && r.Ruta == DetalleDeTurnoFalso.RutaDetalle(TurnoId));
        handler.Requests.Should().NotContain(r =>
            r.Metodo == HttpMethod.Get && r.Ruta == DetalleDeTurnoFalso.RutaCatalogo);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "El turno no tiene franjas";
        var (tool, _) = CrearTool(HttpStatusCode.Conflict, cuerpo);

        var resultado = await Ejecutar(tool, TurnoSinFranjasId, "3:00");

        resultado.Should().Be(string.Format(ModificarVentanaDeEntradaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_TraduceElRechazoDelDominio_Cuando404()
    {
        const string cuerpo = "No existe el turno";
        var (tool, _) = CrearTool(HttpStatusCode.NotFound, cuerpo);

        var resultado = await Ejecutar(tool, TurnoId, "3:00");

        resultado.Should().Be(string.Format(ModificarVentanaDeEntradaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_TraduceElRechazoDelDominio_Cuando400()
    {
        const string cuerpo = "Amplitud invalida";
        var (tool, _) = CrearTool(HttpStatusCode.BadRequest, cuerpo);

        var resultado = await Ejecutar(tool, TurnoId, "3:00");

        resultado.Should().Be(string.Format(ModificarVentanaDeEntradaTool.Mensajes.RechazoDelDominio, cuerpo));
    }
}
