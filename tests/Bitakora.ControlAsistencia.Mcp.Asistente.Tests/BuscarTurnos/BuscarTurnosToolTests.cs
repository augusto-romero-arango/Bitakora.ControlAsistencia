using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.BuscarTurnos;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.BuscarTurnos;

public class BuscarTurnosToolTests
{
    private static async Task<(string Texto, HandlerEnlatado Handler)> EjecutarTexto(
        string json, string? nombre = null, string? horario = null, string? sede = null)
    {
        var (cliente, handler) = ClienteFalso.Con(json);
        var tool = new BuscarTurnosTool(new ProgramacionApi(cliente));

        var texto = await tool.Run(null!, nombre, horario, sede, TestContext.Current.CancellationToken);

        return (texto, handler);
    }

    private static string Catalogo => Fixtures.Leer("BuscarTurnos", "buscar-turnos.json");

    private static JsonObject Parsear(string texto) => JsonNode.Parse(texto)!.AsObject();

    private static IEnumerable<string> Nombres(JsonObject json) =>
        json["turnos"]!.AsArray().Select(t => t!["nombre"]!.GetValue<string>());

    [Fact]
    public async Task BuscarTurnos_CombinaNombreHorarioYSedeEnAnd_CuandoLosTresCriteriosEstanPresentes()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, "vigilancia", "06:00-14:00", "norte");
        var json = Parsear(texto);

        Nombres(json).Should().Equal("Vigilancia Diurno", "Vigilancia Tarde", "Vigilancia Madrugada");
        json["mostrando"]!.GetValue<int>().Should().Be(3);
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("total").Should().BeFalse();
        json.ContainsKey("nota").Should().BeFalse();
    }

    [Fact]
    public async Task BuscarTurnos_TraeIdNombreHorarioYCoincidePor_CuandoElTurnoEsExacto()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, "vigilancia", "06:00-14:00", "norte");

        var primero = Parsear(texto)["turnos"]![0]!.AsObject();
        primero["id"]!.GetValue<string>().Should().Be("id-2");
        primero["nombre"]!.GetValue<string>().Should().Be("Vigilancia Diurno");
        primero["horario"]!.GetValue<string>().Should().Be("06:00-14:00");
        var motivos = primero["coincidePor"]!.AsArray().Select(m => m!.GetValue<string>()).ToList();
        motivos.Should().HaveCount(3);
        motivos.Should().Contain(string.Format(BuscarTurnosTool.Mensajes.CoincidePorHorarioExacto, "06:00-14:00"));
        motivos.Should().Contain(string.Format(BuscarTurnosTool.Mensajes.CoincidePorSede, "Norte"));
        motivos.Should().Contain(m => m.StartsWith("nombre:") && m.Contains("vigilancia"));
        primero.ContainsKey("esDescanso").Should().BeFalse();
        primero.ContainsKey("enConstruccion").Should().BeFalse();
    }

    [Fact]
    public async Task BuscarTurnos_RotulaCercanoYSeCruza_CuandoLaFranjaNoEsExacta()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, horario: "06:00-14:00");
        var turnos = Parsear(texto)["turnos"]!.AsArray();

        turnos.Select(t => t!["nombre"]!.GetValue<string>()).Should()
            .Equal("Cocina Manana", "Vigilancia Diurno", "Vigilancia Tarde", "Vigilancia Madrugada");
        turnos[2]!["coincidePor"]![0]!.GetValue<string>().Should().Contain("horario");
        turnos[2]!["coincidePor"]![0]!.GetValue<string>().Should().Contain("05:00-13:00");
    }

    [Fact]
    public async Task BuscarTurnos_ExcluyeTurnosSinFranjas_CuandoSePideHorarioOSede()
    {
        var (porHorario, _) = await EjecutarTexto(Catalogo, horario: "06:00");
        var (porSede, _) = await EjecutarTexto(Catalogo, sede: "norte");

        Nombres(Parsear(porHorario)).Should().NotContain(["Descanso Vigilancia", "Vigilancia Nocturno En Construccion"]);
        Nombres(Parsear(porSede)).Should().NotContain(["Descanso Vigilancia", "Vigilancia Nocturno En Construccion"]);
    }

    [Fact]
    public async Task BuscarTurnos_MarcaDescansoYEnConstruccion_CuandoEntranSoloPorNombre()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "vigilancia");
        var turnos = Parsear(texto)["turnos"]!.AsArray();

        turnos.Select(t => t!["nombre"]!.GetValue<string>()).Should().Equal(
            "Descanso Vigilancia", "Turno Nocturno Vigilancia", "Vigilancia Diurno",
            "Vigilancia Madrugada", "Vigilancia Nocturno En Construccion", "Vigilancia Tarde");

        var descanso = turnos[0]!.AsObject();
        descanso["esDescanso"]!.GetValue<bool>().Should().BeTrue();
        descanso.ContainsKey("enConstruccion").Should().BeFalse();

        var enConstruccion = turnos[4]!.AsObject();
        enConstruccion["enConstruccion"]!.GetValue<bool>().Should().BeTrue();
        enConstruccion.ContainsKey("esDescanso").Should().BeFalse();
    }

    [Fact]
    public async Task BuscarTurnos_PoneMasPalabrasPrimero_CuandoElNombreTieneVariasCoincidencias()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "vigilancia nocturno");

        Nombres(Parsear(texto)).Should().StartWith(
            ["Turno Nocturno Vigilancia", "Vigilancia Nocturno En Construccion"]);
    }

    [Fact]
    public async Task BuscarTurnos_FiltraPorCodigoDeSede_CuandoSePideElCodigo()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, sede: "sede-centro");

        Nombres(Parsear(texto)).Should().Equal("Cocina Manana");
    }

    [Fact]
    public async Task BuscarTurnos_ConsumeElCatalogoSinTake_CuandoBusca()
    {
        var (_, handler) = await EjecutarTexto(Catalogo, nombre: "vigilancia");

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be("/api/programacion/turnos");
        handler.UltimaRequest.RequestUri.Query.Should().NotContain("take");
    }

    [Fact]
    public async Task BuscarTurnos_RespondeConFraseSinLlamarAlServidor_CuandoNoHayCriterios()
    {
        var (texto, handler) = await EjecutarTexto(Catalogo, "  ", null, "");

        texto.Should().Be(BuscarTurnosTool.Mensajes.FaltaCriterio);
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task BuscarTurnos_RespondeConFraseSinLlamarAlServidor_CuandoElHorarioEstaMalFormado()
    {
        var (texto, handler) = await EjecutarTexto(Catalogo, horario: "6am");

        texto.Should().Be(string.Format(BuscarTurnosTool.Mensajes.HorarioMalFormado, "6am"));
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task BuscarTurnos_RespondeConFraseDelCriterio_CuandoNoHayCoincidencias()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "zzzzzz");

        texto.Should().Be(string.Format(BuscarTurnosTool.Mensajes.SinCoincidencias, "nombre: zzzzzz"));
    }

    [Fact]
    public async Task BuscarTurnos_TruncaAVeinteConNota_CuandoHayVeinticincoCoincidencias()
    {
        var elementos = Enumerable.Range(1, 25).Select(i => new
        {
            id = $"id-{i:00}",
            nombre = $"Turno Vigilancia {i:00}",
            esDescanso = false,
            horarioResumido = "06:00-14:00",
            franjas = new[]
            {
                new
                {
                    horaInicio = "06:00:00", horaFin = "14:00:00", diaOffsetFin = 0,
                    descansos = Array.Empty<object>(), extras = Array.Empty<object>(),
                    sedeId = (string?)null, nombreSede = (string?)null, descripcion = ""
                }
            },
            descripcion = "",
            completo = true
        });
        var json = JsonSerializer.Serialize(new { elementos, siguienteCursor = (string?)null });

        var (texto, _) = await EjecutarTexto(json, nombre: "vigilancia");
        var raiz = Parsear(texto);

        raiz["mostrando"]!.GetValue<int>().Should().Be(20);
        raiz["turnos"]!.AsArray().Should().HaveCount(20);
        raiz["nota"]!.GetValue<string>().Should().Be(BuscarTurnosTool.Mensajes.NotaTruncado);
        raiz.ContainsKey("siguienteCursor").Should().BeFalse();
        raiz.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task BuscarTurnos_SenalaConVentanaDeEntrada_SoloEnElTurnoConVentana()
    {
        var (texto, _) = await EjecutarTexto(
            Fixtures.Leer("BuscarTurnos", "buscar-turnos-con-ventana.json"), "oficina");

        var turnos = Parsear(texto)["turnos"]!.AsArray();
        var conVentana = turnos.Single(t => t!["nombre"]!.GetValue<string>() == "Oficina")!.AsObject();
        var fijo = turnos.Single(t => t!["nombre"]!.GetValue<string>() == "Oficina Fija")!.AsObject();
        conVentana["conVentanaDeEntrada"]!.GetValue<bool>().Should().BeTrue();
        fijo.ContainsKey("conVentanaDeEntrada").Should().BeFalse();
    }
}
