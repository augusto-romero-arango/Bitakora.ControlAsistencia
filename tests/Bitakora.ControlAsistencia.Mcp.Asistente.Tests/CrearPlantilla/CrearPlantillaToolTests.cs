using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.CrearPlantilla;

public class CrearPlantillaToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaPlantillas = "/api/programacion/plantillas-turnos";
    private const string RutaJornadas = "/api/programacion/jornadas";
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private const string TurnoManana = "8f14e45f-ceea-4b3c-8f0a-000000000001";
    private const string TurnoTarde = "8f14e45f-ceea-4b3c-8f0a-000000000002";
    private const string TurnoInexistente = "8f14e45f-ceea-4b3c-8f0a-0000000000e1";
    private const string IdHomonimo = "8f14e45f-ceea-4b3c-8f0a-0000000000a1";

    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");

    private sealed record Fakes(CrearPlantillaTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        string? catalogoJson = null,
        HttpStatusCode statusPost = HttpStatusCode.Created,
        string cuerpoPost = "",
        Func<string, HttpStatusCode>? statusPut = null,
        HttpStatusCode statusJornada = HttpStatusCode.OK)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, catalogoJson ?? TurnosJson);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoManana);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoTarde);
        handler.RespondeDetalleDeTurno(TurnosJson, TurnoInexistente);
        handler.Responde(HttpMethod.Get, $"{RutaJornadas}/{JornadaId}", statusJornada,
            Fixtures.Leer("AsignarJornadaAPlantilla", "obtener-jornada.json"));
        handler.Responde(HttpMethod.Post, RutaPlantillas, statusPost, cuerpoPost);
        handler.RespondeConPrefijo(
            HttpMethod.Put, $"{RutaPlantillas}/",
            (r, _) => new HttpResponseMessage(statusPut?.Invoke(r.RequestUri!.AbsolutePath) ?? HttpStatusCode.NoContent));
        handler.Responde(HttpMethod.Post, RutaTurnos, HttpStatusCode.Created, "");
        handler.RespondeConPrefijo(HttpMethod.Post, $"{RutaTurnos}/", HttpStatusCode.NoContent);

        var api = new ProgramacionApi(cliente);
        return new Fakes(
            new CrearPlantillaTool(api, new ResolutorTurnoPorId(api), new ResolutorTurnoPorNombre(api)), handler);
    }

    private static Task<string> Ejecutar(
        Fakes fakes, string dias, int? diasDelCiclo = 3, string nombre = "Ciclo Cocina", string? jornadaId = null) =>
        fakes.Tool.Run(null!, nombre, diasDelCiclo, dias, jornadaId, TestContext.Current.CancellationToken);

    private static string DosDiasPorId =>
        $$"""[{"dia":1,"turno_id":"{{TurnoManana}}"},{"dia":2,"turno_id":"{{TurnoTarde}}"}]""";

    private static void NoEscribioNada(Fakes fakes) =>
        fakes.Handler.Requests.Should().OnlyContain(r => r.Metodo == HttpMethod.Get);

    private static string FranjaJson(string inicio, string fin, int offsetFin) =>
        $$"""{"horaInicio":"{{inicio}}:00","horaFin":"{{fin}}:00","diaOffsetFin":{{offsetFin}},"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":""}""";

    private static string CatalogoCon(string nombre, string franjas) =>
        $$"""{"elementos":[{"id":"{{IdHomonimo}}","nombre":"{{nombre}}","esDescanso":false,"horarioResumido":"","franjas":[{{franjas}}],"descripcion":"","completo":true}]}""";

    // CA-1
    [Fact]
    public async Task CrearPlantilla_HaceUnPostYUnPutPorDiaEnOrden_CuandoTodosLosTurnosExisten()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDiasPorId);

        var post = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaPlantillas);
        var postBody = JsonNode.Parse(post.Cuerpo!)!;
        var plantillaId = postBody["plantillaId"]!.GetValue<string>();
        Guid.TryParse(plantillaId, out _).Should().BeTrue();
        postBody["nombre"]!.GetValue<string>().Should().Be("Ciclo Cocina");
        postBody["diasDelCiclo"]!.GetValue<int>().Should().Be(3);

        var puts = fakes.Handler.Requests.Where(r => r.Metodo == HttpMethod.Put).ToList();
        puts.Select(r => r.Ruta).Should().Equal(
            $"{RutaPlantillas}/{plantillaId}/dias/1", $"{RutaPlantillas}/{plantillaId}/dias/2");
        JsonNode.Parse(puts[0].Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(TurnoManana);
        JsonNode.Parse(puts[1].Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(TurnoTarde);

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(CrearPlantillaTool.Mensajes.ResultadoPlantillaCreada);
        json["plantilla"]!["id"]!.GetValue<string>().Should().Be(plantillaId);
        json["plantilla"]!["nombre"]!.GetValue<string>().Should().Be("Ciclo Cocina");
        json["plantilla"]!["diasDelCiclo"]!.GetValue<int>().Should().Be(3);
        json["diasAsignados"]!.GetValue<int>().Should().Be(2);
        json["completa"]!.GetValue<bool>().Should().BeFalse("solo 2 de 3 dias tienen turno");
        json.AsObject().ContainsKey("diasRechazados").Should().BeFalse();
        json.AsObject().ContainsKey("turnosInline").Should().BeFalse();
        json["nota"]!.GetValue<string>().Should().Be(CrearPlantillaTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task CrearPlantilla_MarcaCompleta_CuandoSeAsignanLosNDias()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":1,"turno_id":"{{TurnoManana}}"},{"dia":2,"turno_id":"{{TurnoTarde}}"}]""";

        var resultado = await Ejecutar(fakes, dias, diasDelCiclo: 2);

        var json = JsonNode.Parse(resultado)!;
        json["diasAsignados"]!.GetValue<int>().Should().Be(2);
        json["completa"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task CrearPlantilla_LeeUnSoloDetalle_CuandoUnTurnoIdSeRepiteEnVariosDias()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":1,"turno_id":"{{TurnoManana}}"},{"dia":2,"turno_id":"{{TurnoManana}}"}]""";

        await Ejecutar(fakes, dias);

        fakes.Handler.Requests.Where(r => r.Metodo == HttpMethod.Get).Should().ContainSingle()
            .Which.Ruta.Should().Be($"{RutaTurnos}/{TurnoManana}");
        fakes.Handler.Requests.Count(r => r.Metodo == HttpMethod.Put).Should().Be(2);
    }

    [Fact]
    public async Task CrearPlantilla_LeeLaJornadaAntesDelPostYEnviaJornadaId_CuandoSeIndicaJornadaId()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDiasPorId, jornadaId: JornadaId);

        fakes.Handler.Requests[0].Ruta.Should().Be($"{RutaJornadas}/{JornadaId}");
        var post = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaPlantillas);
        JsonNode.Parse(post.Cuerpo!)!["jornadaId"]!.GetValue<string>().Should().Be(JornadaId);
        JsonNode.Parse(resultado)!["jornada"]!["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
    }

    // CA-2
    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoNombreEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDiasPorId, nombre: " ");

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.CampoObligatorio, "nombre"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoFaltaDiasDelCiclo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDiasPorId, diasDelCiclo: null);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.CampoObligatorio, "dias_del_ciclo"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoDiasDelCicloEsMenorQueUno()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDiasPorId, diasDelCiclo: 0);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.DiasDelCicloFueraDeRango, 0));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoDiasDelCicloSuperaCuarentaYDos()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDiasPorId, diasDelCiclo: 43);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.DiasDelCicloFueraDeRango, 43));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoDiasEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, "");

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.CampoObligatorio, "dias"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoDiasNoEsJsonValido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, "no es json");

        resultado.Should().Be(CrearPlantillaTool.Mensajes.DiasJsonInvalido);
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoDiasEsUnaListaVacia()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, "[]");

        resultado.Should().Be(CrearPlantillaTool.Mensajes.DiasVacio);
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoDiaNoEsEntero()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":"lunes","turno_id":"{{TurnoManana}}"}]""";

        var resultado = await Ejecutar(fakes, dias);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.DiaInvalido, "lunes"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoDiaNoEstaEnElJson()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"turno_id":"{{TurnoManana}}"}]""";

        var resultado = await Ejecutar(fakes, dias);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.DiaInvalido, ""));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoDiaEsMayorQueLosDiasDelCiclo()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":4,"turno_id":"{{TurnoManana}}"}]""";

        var resultado = await Ejecutar(fakes, dias, diasDelCiclo: 3);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.DiaFueraDeRango, 4, 3));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoDiaEsCero()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":0,"turno_id":"{{TurnoManana}}"}]""";

        var resultado = await Ejecutar(fakes, dias, diasDelCiclo: 3);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.DiaFueraDeRango, 0, 3));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoUnDiaSeRepite()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":2,"turno_id":"{{TurnoManana}}"},{"dia":2,"turno_id":"{{TurnoTarde}}"}]""";

        var resultado = await Ejecutar(fakes, dias);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.DiaDuplicado, 2));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoLaEntradaTraeTurnoIdYFranja()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":1,"turno_id":"{{TurnoManana}}","franja":"07:00-15:00"}]""";

        var resultado = await Ejecutar(fakes, dias);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.TurnoYFranjaExcluyentes, 1));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoLaEntradaNoTraeTurnoNiFranja()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, """[{"dia":1}]""");

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.TurnoOFranjaObligatorio, 1));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoLaEntradaTraeTurnoPorNombre()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, """[{"dia":1,"turno":"Cocina Manana"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.TurnoPorNombreNoSoportado, 1));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoTurnoIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, """[{"dia":1,"turno_id":"Cocina Manana"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.TurnoIdInvalido, "Cocina Manana"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoLaFranjaNoTieneFormatoValido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, """[{"dia":1,"franja":"manana"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.FranjaInvalida, 1, "manana"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RespondeTurnosNoExistenSinEscribirNada_CuandoAlgunTurnoIdNoExiste()
    {
        var fakes = CrearTool();
        var dias = $$"""[{"dia":1,"turno_id":"{{TurnoInexistente}}"},{"dia":2,"turno_id":"{{TurnoTarde}}"}]""";

        var resultado = await Ejecutar(fakes, dias);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.TurnosNoExisten, TurnoInexistente));
        NoEscribioNada(fakes);
    }

    [Fact]
    public async Task CrearPlantilla_RechazaSinLlamarAlBackend_CuandoJornadaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, DosDiasPorId, jornadaId: "no-es-guid");

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantilla_RespondeJornadaNoExisteSinEscribirNada_Cuando404DeLaJornada()
    {
        var fakes = CrearTool(statusJornada: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes, DosDiasPorId, jornadaId: JornadaId);

        resultado.Should().Be(CrearPlantillaTool.Mensajes.JornadaNoExiste);
        NoEscribioNada(fakes);
    }

    // CA-3
    [Fact]
    public async Task CrearPlantilla_CreaElTurnoInlineYAsignaElDia_CuandoLaFranjaNoTieneHomonimo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, """[{"dia":1,"franja":"7:00-17:00"}]""");

        var turno = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaTurnos);
        var turnoBody = JsonNode.Parse(turno.Cuerpo!)!;
        turnoBody["nombre"]!.GetValue<string>().Should().Be("07:00-17:00");
        var turnoId = turnoBody["turnoId"]!.GetValue<string>();

        var franja = fakes.Handler.Requests.Single(r => r.Ruta.EndsWith(":agregar-franja", StringComparison.Ordinal));
        franja.Ruta.Should().Be($"{RutaTurnos}/{turnoId}:agregar-franja");

        var put = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        JsonNode.Parse(put.Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(turnoId);

        var inline = JsonNode.Parse(resultado)!["turnosInline"]!.AsArray().Single()!;
        inline["nombre"]!.GetValue<string>().Should().Be("07:00-17:00");
        inline["accion"]!.GetValue<string>().Should().Be("creo");
    }

    [Fact]
    public async Task CrearPlantilla_ReutilizaElTurnoSinEscribirTurnos_CuandoElHomonimoTieneLaMismaFranja()
    {
        var fakes = CrearTool(CatalogoCon("07:00-17:00", FranjaJson("07:00", "17:00", 0)));

        var resultado = await Ejecutar(fakes, """[{"dia":1,"franja":"07:00-17:00"}]""");

        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post && r.Ruta.StartsWith(RutaTurnos));
        var put = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        JsonNode.Parse(put.Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(IdHomonimo);
        JsonNode.Parse(resultado)!["turnosInline"]!.AsArray().Single()!["accion"]!.GetValue<string>().Should().Be("reutilizo");
    }

    [Fact]
    public async Task CrearPlantilla_CompletaElTurnoSinCrearlo_CuandoElHomonimoNoTieneFranjas()
    {
        var fakes = CrearTool(CatalogoCon("07:00-17:00", ""));

        var resultado = await Ejecutar(fakes, """[{"dia":1,"franja":"07:00-17:00"}]""");

        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaTurnos);
        fakes.Handler.Requests.Should().ContainSingle(r => r.Ruta == $"{RutaTurnos}/{IdHomonimo}:agregar-franja");
        var put = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        JsonNode.Parse(put.Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(IdHomonimo);
        JsonNode.Parse(resultado)!["turnosInline"]!.AsArray().Single()!["accion"]!.GetValue<string>().Should().Be("completo");
    }

    [Fact]
    public async Task CrearPlantilla_AbortaSinEscribir_CuandoElHomonimoTieneOtraFranja()
    {
        var fakes = CrearTool(CatalogoCon("07:00-17:00", FranjaJson("08:00", "18:00", 0)));

        var resultado = await Ejecutar(fakes, """[{"dia":1,"franja":"07:00-17:00"}]""");

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.TurnoInlineEnConflicto, "07:00-17:00"));
        NoEscribioNada(fakes);
    }

    // CA-4
    [Fact]
    public async Task CrearPlantilla_ListaElDiaRechazadoYSigueConLosDemas_CuandoUnPutRespondeConflicto()
    {
        var fakes = CrearTool(statusPut: ruta => ruta.EndsWith("/dias/2", StringComparison.Ordinal)
            ? HttpStatusCode.Conflict
            : HttpStatusCode.NoContent);

        var resultado = await Ejecutar(fakes, DosDiasPorId);

        fakes.Handler.Requests.Count(r => r.Metodo == HttpMethod.Put).Should().Be(2);
        var json = JsonNode.Parse(resultado)!;
        json["diasAsignados"]!.GetValue<int>().Should().Be(1);
        json["completa"]!.GetValue<bool>().Should().BeFalse();
        var rechazado = json["diasRechazados"]!.AsArray().Single()!;
        rechazado["dia"]!.GetValue<int>().Should().Be(2);
        rechazado["motivo"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CrearPlantilla_TraduceElRechazoDelDominioSinPut_Cuando409DelPost()
    {
        const string cuerpo = "Ya existe una plantilla con el nombre 'Ciclo Cocina'";
        var fakes = CrearTool(statusPost: HttpStatusCode.Conflict, cuerpoPost: cuerpo);

        var resultado = await Ejecutar(fakes, DosDiasPorId);

        resultado.Should().Be(string.Format(CrearPlantillaTool.Mensajes.RechazoDelDominio, cuerpo));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }
}
