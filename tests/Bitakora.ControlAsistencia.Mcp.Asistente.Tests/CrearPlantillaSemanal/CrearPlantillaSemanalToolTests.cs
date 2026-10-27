using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.CrearPlantillaSemanal;

public class CrearPlantillaSemanalToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaPlantillas = "/api/programacion/plantillas-turnos";
    private const string RutaPlantillasSemanales = "/api/programacion/plantillas-semanales";
    private const string RutaJornadas = "/api/programacion/jornadas";
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private const string TurnoManana = "8f14e45f-ceea-4b3c-8f0a-000000000001";
    private const string TurnoTarde = "8f14e45f-ceea-4b3c-8f0a-000000000002";
    private const string TurnoInexistente = "8f14e45f-ceea-4b3c-8f0a-0000000000e1";

    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");

    private sealed record Fakes(CrearPlantillaSemanalTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(Func<string, HttpStatusCode>? statusPut = null)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, TurnosJson);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoManana);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoTarde);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoInexistente);
        handler.Responde(HttpMethod.Get, $"{RutaJornadas}/{JornadaId}", HttpStatusCode.OK,
            Fixtures.Leer("AsignarJornadaAPlantillaSemanal", "obtener-jornada.json"));
        handler.Responde(HttpMethod.Post, RutaPlantillas, HttpStatusCode.Created, "");
        handler.RespondeConPrefijo(
            HttpMethod.Put, $"{RutaPlantillas}/",
            (r, _) => new HttpResponseMessage(statusPut?.Invoke(r.RequestUri!.AbsolutePath) ?? HttpStatusCode.NoContent));
        handler.Responde(HttpMethod.Post, RutaTurnos, HttpStatusCode.Created, "");
        handler.RespondeConPrefijo(HttpMethod.Post, $"{RutaTurnos}/", HttpStatusCode.NoContent);

        var api = new ProgramacionApi(cliente);
        return new Fakes(
            new CrearPlantillaSemanalTool(api, new ResolutorTurnoPorId(api), new ResolutorTurnoPorNombre(api)), handler);
    }

    private static Task<string> Ejecutar(
        Fakes fakes, string dias, string nombre = "Semana Cocina", string? jornadaId = null) =>
        fakes.Tool.Run(null!, nombre, dias, jornadaId, TestContext.Current.CancellationToken);

    private static string DosDias =>
        $$"""[{"dia":"lunes","turno_id":"{{TurnoManana}}"},{"dia":"martes","turno_id":"{{TurnoTarde}}"}]""";

    private static void NoEscribioNada(Fakes fakes) =>
        fakes.Handler.Requests.Should().OnlyContain(r => r.Metodo == HttpMethod.Get);

    [Fact]
    public async Task CrearPlantillaSemanal_CreaLaPlantillaDeTurnosDeSieteDias_CuandoTodosLosTurnosExisten()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDias);

        var post = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaPlantillas);
        var postBody = JsonNode.Parse(post.Cuerpo!)!;
        var plantillaId = postBody["plantillaId"]!.GetValue<string>();
        postBody["nombre"]!.GetValue<string>().Should().Be("Semana Cocina");
        postBody["diasDelCiclo"]!.GetValue<int>().Should().Be(7);
        fakes.Handler.Requests.Should().NotContain(r => r.Ruta.StartsWith(RutaPlantillasSemanales));

        var puts = fakes.Handler.Requests.Where(r => r.Metodo == HttpMethod.Put).ToList();
        puts.Select(r => r.Ruta).Should().Equal(
            $"{RutaPlantillas}/{plantillaId}/dias/1", $"{RutaPlantillas}/{plantillaId}/dias/2");
        JsonNode.Parse(puts[0].Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(TurnoManana);
        JsonNode.Parse(puts[1].Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(TurnoTarde);

        var json = JsonNode.Parse(resultado)!;
        json["plantilla"]!["id"]!.GetValue<string>().Should().Be(plantillaId);
        json["plantilla"]!["nombre"]!.GetValue<string>().Should().Be("Semana Cocina");
        json["diasAsignados"]!.GetValue<int>().Should().Be(2);
        json["completa"]!.GetValue<bool>().Should().BeFalse("solo 2 de 7 dias tienen turno");
        json.AsObject().ContainsKey("diasRechazados").Should().BeFalse();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_AsignaCadaDiaAlNumeroDelCiclo_CuandoElDiaEsNombreONumero()
    {
        var fakes = CrearTool();
        var dias = $$"""
            [{"dia":"miercoles","turno_id":"{{TurnoManana}}"},{"dia":7,"turno_id":"{{TurnoTarde}}"},{"dia":"1","turno_id":"{{TurnoManana}}"}]
            """;

        await Ejecutar(fakes, dias);

        fakes.Handler.Requests.Where(r => r.Metodo == HttpMethod.Put)
            .Select(r => r.Ruta[(r.Ruta.LastIndexOf('/') + 1)..])
            .Should().Equal("3", "7", "1");
    }

    [Fact]
    public async Task CrearPlantillaSemanal_MarcaCompleta_CuandoSeAsignanLosSieteDias()
    {
        var fakes = CrearTool();
        var nombres = new[] { "lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo" };
        var dias = "[" + string.Join(",", nombres.Select(n => $$"""{"dia":"{{n}}","turno_id":"{{TurnoManana}}"}""")) + "]";

        var resultado = await Ejecutar(fakes, dias);

        var json = JsonNode.Parse(resultado)!;
        json["diasAsignados"]!.GetValue<int>().Should().Be(7);
        json["completa"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_NombraElDiaRechazadoDeLunesADomingo_CuandoElDominioRechazaUnPut()
    {
        var fakes = CrearTool(statusPut: ruta => ruta.EndsWith("/dias/2") ? HttpStatusCode.Conflict : HttpStatusCode.NoContent);

        var resultado = await Ejecutar(fakes, DosDias);

        var json = JsonNode.Parse(resultado)!;
        json["diasAsignados"]!.GetValue<int>().Should().Be(1);
        json["diasRechazados"]!.AsArray().Single()!["dia"]!.GetValue<string>().Should().Be("martes");
        json["completa"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_LeeLaJornadaAntesDelPostYEnviaJornadaId_CuandoSeIndicaJornadaId()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDias, jornadaId: JornadaId);

        fakes.Handler.Requests[0].Ruta.Should().Be($"{RutaJornadas}/{JornadaId}");
        var post = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaPlantillas);
        JsonNode.Parse(post.Cuerpo!)!["jornadaId"]!.GetValue<string>().Should().Be(JornadaId);
        JsonNode.Parse(resultado)!["jornada"]!["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_CreaElTurnoInlineYAsignaElDia_CuandoLaEntradaTraeFranja()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, """[{"dia":"martes","franja":"07:00-15:00"}]""");

        fakes.Handler.Requests.Should().Contain(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaTurnos);
        fakes.Handler.Requests.Should().Contain(r => r.Metodo == HttpMethod.Put && r.Ruta.EndsWith("/dias/2"));
        JsonNode.Parse(resultado)!["turnosInline"]!.AsArray().Should().ContainSingle();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinEscribirRemitiendoACrearPlantilla_CuandoLaEntradaTraeSemanaDistintaDeUno()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"semana":2,"dia":"lunes","turno_id":"{{TurnoManana}}"}]""";

        var resultado = await Ejecutar(fakes, dias);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.SemanaDistintaDeUno, 2));
        resultado.Should().Contain("crear_plantilla").And.Contain("crear_plantilla_rotativa");
        NoEscribioNada(fakes);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_AceptaLaEntrada_CuandoLaSemanaEsUno()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"semana":1,"dia":"lunes","turno_id":"{{TurnoManana}}"}]""";

        var resultado = await Ejecutar(fakes, dias);

        JsonNode.Parse(resultado)!["diasAsignados"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinEscribir_CuandoElDiaEsInvalido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, $$"""[{"dia":"quinto","turno_id":"{{TurnoManana}}"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.DiaDesconocido, "quinto"));
        NoEscribioNada(fakes);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinEscribir_CuandoElDiaSeRepiteConOtroFormato()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":"lunes","turno_id":"{{TurnoManana}}"},{"dia":1,"turno_id":"{{TurnoTarde}}"}]""";

        var resultado = await Ejecutar(fakes, dias);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.DiaDuplicado, 1, 1));
        NoEscribioNada(fakes);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinEscribir_CuandoNombreEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDias, nombre: " ");

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.CampoObligatorio, "nombre"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinEscribir_CuandoDiasNoEsJsonValido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, "no es json");

        resultado.Should().Be(CrearPlantillaSemanalTool.Mensajes.DiasJsonInvalido);
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinEscribir_CuandoLaEntradaTraeTurnoIdYFranja()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":"lunes","turno_id":"{{TurnoManana}}","franja":"07:00-15:00"}]""";

        var resultado = await Ejecutar(fakes, dias);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.TurnoYFranjaExcluyentes, 1, "lunes"));
        NoEscribioNada(fakes);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_NoCreaNada_CuandoUnTurnoNoExiste()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":"lunes","turno_id":"{{TurnoManana}}"},{"dia":"martes","turno_id":"{{TurnoInexistente}}"}]""";

        var resultado = await Ejecutar(fakes, dias);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.TurnosNoExisten, TurnoInexistente));
        NoEscribioNada(fakes);
    }
}
