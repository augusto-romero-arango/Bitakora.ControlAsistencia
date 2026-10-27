using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaRotativa;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.CrearPlantillaRotativa;

public class CrearPlantillaRotativaToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaPlantillas = "/api/programacion/plantillas-turnos";
    private const string RutaJornadas = "/api/programacion/jornadas";
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private const string TurnoManana = "8f14e45f-ceea-4b3c-8f0a-000000000001";
    private const string TurnoTarde = "8f14e45f-ceea-4b3c-8f0a-000000000002";
    private const string TurnoInexistente = "8f14e45f-ceea-4b3c-8f0a-0000000000e1";

    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");

    private sealed record Fakes(CrearPlantillaRotativaTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        HttpStatusCode statusPost = HttpStatusCode.Created,
        string cuerpoPost = "",
        Func<string, HttpStatusCode>? statusPut = null,
        HttpStatusCode statusJornada = HttpStatusCode.OK)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, TurnosJson);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoManana);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoTarde);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoInexistente);
        handler.Responde(HttpMethod.Get, $"{RutaJornadas}/{JornadaId}", statusJornada,
            Fixtures.Leer("AsignarJornadaAPlantillaSemanal", "obtener-jornada.json"));
        handler.Responde(HttpMethod.Post, RutaPlantillas, statusPost, cuerpoPost);
        handler.RespondeConPrefijo(
            HttpMethod.Put, $"{RutaPlantillas}/",
            (r, _) => new HttpResponseMessage(statusPut?.Invoke(r.RequestUri!.AbsolutePath) ?? HttpStatusCode.NoContent));
        handler.Responde(HttpMethod.Post, RutaTurnos, HttpStatusCode.Created, "");
        handler.RespondeConPrefijo(HttpMethod.Post, $"{RutaTurnos}/", HttpStatusCode.NoContent);

        var api = new ProgramacionApi(cliente);
        return new Fakes(
            new CrearPlantillaRotativaTool(api, new ResolutorTurnoPorId(api), new ResolutorTurnoPorNombre(api)), handler);
    }

    private static Task<string> Ejecutar(
        Fakes fakes, string bloques, string nombre = "Rotativa 6x2", string? jornadaId = null) =>
        fakes.Tool.Run(null!, nombre, bloques, jornadaId, TestContext.Current.CancellationToken);

    private static string SeisYDos =>
        $$"""[{"dias":6,"turno_id":"{{TurnoManana}}"},{"dias":2,"turno_id":"{{TurnoTarde}}"}]""";

    private static void NoEscribioNada(Fakes fakes) =>
        fakes.Handler.Requests.Should().OnlyContain(r => r.Metodo == HttpMethod.Get);

    // CA-1
    [Fact]
    public async Task CrearPlantillaRotativa_ExpandeLosBloquesALosDiasDelCiclo_CuandoLosBloquesSonValidos()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, SeisYDos);

        var post = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaPlantillas);
        var postBody = JsonNode.Parse(post.Cuerpo!)!;
        var plantillaId = postBody["plantillaId"]!.GetValue<string>();
        postBody["nombre"]!.GetValue<string>().Should().Be("Rotativa 6x2");
        postBody["diasDelCiclo"]!.GetValue<int>().Should().Be(8);

        var puts = fakes.Handler.Requests.Where(r => r.Metodo == HttpMethod.Put).ToList();
        puts.Select(r => r.Ruta).Should().Equal(
            Enumerable.Range(1, 8).Select(d => $"{RutaPlantillas}/{plantillaId}/dias/{d}"));
        puts.Select(r => JsonNode.Parse(r.Cuerpo!)!["turnoId"]!.GetValue<string>()).Should().Equal(
            [TurnoManana, TurnoManana, TurnoManana, TurnoManana, TurnoManana, TurnoManana, TurnoTarde, TurnoTarde]);

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(CrearPlantillaRotativaTool.Mensajes.ResultadoPlantillaCreada);
        json["plantilla"]!["id"]!.GetValue<string>().Should().Be(plantillaId);
        json["plantilla"]!["diasDelCiclo"]!.GetValue<int>().Should().Be(8);
        var bloques = json["bloques"]!.AsArray();
        bloques.Select(b => b!["dias"]!.GetValue<string>()).Should().Equal("1-6", "7-8");
        bloques.Select(b => b!["turno"]!.GetValue<string>()).Should().Equal("Cocina Manana", "Cocina Tarde");
        json["diasAsignados"]!.GetValue<int>().Should().Be(8);
        json["completa"]!.GetValue<bool>().Should().BeTrue();
        json.AsObject().ContainsKey("diasRechazados").Should().BeFalse();
        json.AsObject().ContainsKey("turnosEnLinea").Should().BeFalse();
        json.AsObject().ContainsKey("jornada").Should().BeFalse();
        json["nota"]!.GetValue<string>().Should().Be(CrearPlantillaRotativaTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RotulaUnDiaComoRangoDeUnSoloNumero_CuandoElBloqueDuraUnDia()
    {
        var fakes = CrearTool();
        var bloques = $$"""[{"dias":1,"turno_id":"{{TurnoManana}}"},{"dias":2,"turno_id":"{{TurnoTarde}}"}]""";

        var resultado = await Ejecutar(fakes, bloques);

        JsonNode.Parse(resultado)!["bloques"]!.AsArray().Select(b => b!["dias"]!.GetValue<string>())
            .Should().Equal("1", "2-3");
    }

    [Fact]
    public async Task CrearPlantillaRotativa_AdmiteDiasComoTexto_CuandoElModeloLoEnviaEntreComillas()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, $$"""[{"dias":"2","turno_id":"{{TurnoManana}}"}]""");

        JsonNode.Parse(resultado)!["plantilla"]!["diasDelCiclo"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public async Task CrearPlantillaRotativa_AceptaUnCicloDeCuarentaYDosDias_CuandoLosBloquesSumanElMaximo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, $$"""[{"dias":42,"turno_id":"{{TurnoManana}}"}]""");

        JsonNode.Parse(resultado)!["plantilla"]!["diasDelCiclo"]!.GetValue<int>().Should().Be(42);
        fakes.Handler.Requests.Count(r => r.Metodo == HttpMethod.Put).Should().Be(42);
    }

    [Fact]
    public async Task CrearPlantillaRotativa_LeeLaJornadaAntesDelPostYEnviaJornadaId_CuandoSeIndicaJornadaId()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, SeisYDos, jornadaId: JornadaId);

        fakes.Handler.Requests[0].Ruta.Should().Be($"{RutaJornadas}/{JornadaId}");
        var post = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaPlantillas);
        JsonNode.Parse(post.Cuerpo!)!["jornadaId"]!.GetValue<string>().Should().Be(JornadaId);
        JsonNode.Parse(resultado)!["jornada"]!["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
    }

    // CA-2
    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoNombreEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, SeisYDos, nombre: " ");

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.CampoObligatorio, "nombre"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoBloquesEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, "");

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.CampoObligatorio, "bloques"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoBloquesNoEsJsonValido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, "no es json");

        resultado.Should().Be(CrearPlantillaRotativaTool.Mensajes.BloquesJsonInvalido);
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoBloquesEsUnaListaVacia()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, "[]");

        resultado.Should().Be(CrearPlantillaRotativaTool.Mensajes.BloquesVacio);
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoUnBloqueNoTraeDias()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, $$"""[{"turno_id":"{{TurnoManana}}"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.BloqueDiasInvalido, 1, ""));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoDiasNoEsEntero()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, $$"""[{"dias":2.5,"turno_id":"{{TurnoManana}}"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.BloqueDiasInvalido, 1, "2.5"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoDiasEsMenorQueUno()
    {
        var fakes = CrearTool();
        var bloques = $$"""[{"dias":2,"turno_id":"{{TurnoManana}}"},{"dias":0,"turno_id":"{{TurnoTarde}}"}]""";

        var resultado = await Ejecutar(fakes, bloques);

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.BloqueDiasInvalido, 2, "0"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoLaSumaSuperaCuarentaYDos()
    {
        var fakes = CrearTool();
        var bloques = $$"""[{"dias":30,"turno_id":"{{TurnoManana}}"},{"dias":13,"turno_id":"{{TurnoTarde}}"}]""";

        var resultado = await Ejecutar(fakes, bloques);

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.SumaDeDiasFueraDeRango, 43));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoUnBloqueTraeTurnoIdYFranja()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, $$"""[{"dias":2,"turno_id":"{{TurnoManana}}","franja":"07:00-15:00"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.TurnoYFranjaExcluyentes, 1));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoUnBloqueNoTraeTurnoNiFranja()
    {
        var fakes = CrearTool();
        var bloques = $$"""[{"dias":2,"turno_id":"{{TurnoManana}}"},{"dias":2}]""";

        var resultado = await Ejecutar(fakes, bloques);

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.TurnoOFranjaObligatorio, 2));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoUnBloqueTraeTurnoPorNombre()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, """[{"dias":2,"turno":"Cocina Manana"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.TurnoPorNombreNoSoportado, 1));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoTurnoIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, """[{"dias":2,"turno_id":"abc"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.TurnoIdInvalido, "abc"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoLaFranjaNoEsValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, """[{"dias":2,"franja":"manana"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.FranjaInvalida, 1, "manana"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_ListaLosTurnosFaltantesYNoCreaNada_CuandoUnTurnoNoExiste()
    {
        var fakes = CrearTool();
        var bloques = $$"""[{"dias":2,"turno_id":"{{TurnoManana}}"},{"dias":2,"turno_id":"{{TurnoInexistente}}"}]""";
        fakes.Handler.Responde(HttpMethod.Get, $"{RutaTurnos}/{TurnoInexistente}", HttpStatusCode.NotFound, "");

        var resultado = await Ejecutar(fakes, bloques);

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.TurnosNoExisten, TurnoInexistente));
        NoEscribioNada(fakes);
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoJornadaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, SeisYDos, jornadaId: "no-es-guid");

        resultado.Should().Be(string.Format(CrearPlantillaRotativaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_RechazaSinEscribir_CuandoLaJornadaNoExiste()
    {
        var fakes = CrearTool(statusJornada: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes, SeisYDos, jornadaId: JornadaId);

        resultado.Should().Be(CrearPlantillaRotativaTool.Mensajes.JornadaNoExiste);
        NoEscribioNada(fakes);
    }

    // CA-3
    [Fact]
    public async Task CrearPlantillaRotativa_CreaElTurnoYLoAsignaALosDiasDelBloque_CuandoElBloqueTraeFranja()
    {
        var fakes = CrearTool();
        var bloques = $$"""[{"dias":2,"turno_id":"{{TurnoManana}}"},{"dias":1,"franja":"07:00-15:00"}]""";

        var resultado = await Ejecutar(fakes, bloques);

        var turno = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaTurnos);
        var turnoBody = JsonNode.Parse(turno.Cuerpo!)!;
        turnoBody["nombre"]!.GetValue<string>().Should().Be("07:00-15:00");
        var turnoId = turnoBody["turnoId"]!.GetValue<string>();
        fakes.Handler.Requests.Should().ContainSingle(r => r.Ruta == $"{RutaTurnos}/{turnoId}:agregar-franja");

        var puts = fakes.Handler.Requests.Where(r => r.Metodo == HttpMethod.Put).ToList();
        puts.Should().HaveCount(3);
        JsonNode.Parse(puts[2].Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(turnoId);

        var json = JsonNode.Parse(resultado)!;
        json["bloques"]!.AsArray()[1]!["turno"]!.GetValue<string>().Should().Be("07:00-15:00");
        var enLinea = json["turnosEnLinea"]!.AsArray().Single()!;
        enLinea["nombre"]!.GetValue<string>().Should().Be("07:00-15:00");
        enLinea["accion"]!.GetValue<string>().Should().Be("creo");
    }

    [Fact]
    public async Task CrearPlantillaRotativa_ListaLosDiasRechazadosYSigueConLosDemas_CuandoUnPutRespondeConflicto()
    {
        var fakes = CrearTool(statusPut: ruta => ruta.EndsWith("/dias/7", StringComparison.Ordinal)
            ? HttpStatusCode.Conflict
            : HttpStatusCode.NoContent);

        var resultado = await Ejecutar(fakes, SeisYDos);

        fakes.Handler.Requests.Count(r => r.Metodo == HttpMethod.Put).Should().Be(8);
        var json = JsonNode.Parse(resultado)!;
        json["diasAsignados"]!.GetValue<int>().Should().Be(7);
        json["completa"]!.GetValue<bool>().Should().BeFalse();
        var rechazado = json["diasRechazados"]!.AsArray().Single()!;
        rechazado["dia"]!.GetValue<int>().Should().Be(7);
        rechazado["turno"]!.GetValue<string>().Should().Be("Cocina Tarde");
        rechazado["motivo"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CrearPlantillaRotativa_DevuelveElRechazoComoTextoSinAsignarDias_CuandoElPostRespondeConflicto()
    {
        var fakes = CrearTool(statusPost: HttpStatusCode.Conflict, cuerpoPost: """{"detail":"Nombre duplicado"}""");

        var resultado = await Ejecutar(fakes, SeisYDos);

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("Nombre duplicado");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }
}
