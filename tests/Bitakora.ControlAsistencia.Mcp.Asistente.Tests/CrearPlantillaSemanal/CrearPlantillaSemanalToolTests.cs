using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.CrearPlantillaSemanal;

public class CrearPlantillaSemanalToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";

    // Reuso deliberado del fixture de solicitar_programacion_turno (MEF-ADR-0018): mismo catalogo
    // (Cocina Manana id ...001, Cocina Tarde id ...002), mismo resolutor por nombre.
    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");

    private static string Validacion400Json => Fixtures.Leer("CrearPlantillaSemanal", "validacion-400.json");

    // Siete dias validos (lunes..domingo, semana 1), alternando entre los dos turnos del fixture --
    // usada por CA-1 y por los tests de fallo parcial (CA-4) que necesitan la plantilla completa.
    private const string SieteDiasValidosJson = """
        [
          {"semana":1,"dia":"lunes","turno":"Cocina Manana"},
          {"semana":1,"dia":"martes","turno":"Cocina Tarde"},
          {"semana":1,"dia":"miercoles","turno":"Cocina Manana"},
          {"semana":1,"dia":"jueves","turno":"Cocina Tarde"},
          {"semana":1,"dia":"viernes","turno":"Cocina Manana"},
          {"semana":1,"dia":"sabado","turno":"Cocina Tarde"},
          {"semana":1,"dia":"domingo","turno":"Cocina Manana"}
        ]
        """;

    private const string UnaEntradaValidaJson = """[{"semana":1,"dia":"lunes","turno":"Cocina Manana"}]""";

    private sealed record Fakes(CrearPlantillaSemanalTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        string? turnosJson = null,
        HttpStatusCode statusTurnos = HttpStatusCode.OK,
        HttpStatusCode? statusPost = HttpStatusCode.Created,
        string cuerpoPost = "",
        HttpStatusCode statusPut = HttpStatusCode.NoContent,
        HttpStatusCode statusCrearTurno = HttpStatusCode.Created,
        Func<string?, HttpStatusCode>? statusAgregarFranja = null)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaTurnos, statusTurnos, turnosJson ?? TurnosJson);
        if (statusPost is { } status)
            handler.Responde(HttpMethod.Post, RutaPlantillas, status, cuerpoPost);
        handler.RespondeConPrefijo(HttpMethod.Put, $"{RutaPlantillas}/", statusPut, "");
        handler.Responde(HttpMethod.Post, RutaTurnos, statusCrearTurno, "");
        handler.RespondeConPrefijo(
            HttpMethod.Post,
            $"{RutaTurnos}/",
            (_, cuerpo) => new HttpResponseMessage(statusAgregarFranja?.Invoke(cuerpo) ?? HttpStatusCode.NoContent));

        var tool = new CrearPlantillaSemanalTool(new ProgramacionApi(cliente));
        return new Fakes(tool, handler);
    }

    private static string ExtraerPlantillaIdEnviado(Fakes fakes)
    {
        var post = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post);
        return JsonNode.Parse(post.Cuerpo!)!["plantillaId"]!.GetValue<string>();
    }

    // CA-1: un GET, luego un POST con { plantillaId (Guid v7), nombre, semanas }, luego 7 PUT
    // secuenciales en orden 1..7, sin solapamiento -- 9 requests en total, en ese orden exacto.
    [Fact]
    public async Task CrearPlantillaSemanal_HaceUnGetUnPostYSietePutsEnOrden_CuandoTodosLosTurnosExisten()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Semana Tipo Cocina", 1, SieteDiasValidosJson, TestContext.Current.CancellationToken);

        var plantillaIdEnviado = ExtraerPlantillaIdEnviado(fakes);
        Guid.TryParse(plantillaIdEnviado, out _).Should().BeTrue("plantillaId debe ser un Guid v7 valido");

        var esperado = new List<(HttpMethod, string)> { (HttpMethod.Get, RutaTurnos), (HttpMethod.Post, RutaPlantillas) };
        for (var dia = 1; dia <= 7; dia++)
            esperado.Add((HttpMethod.Put, $"{RutaPlantillas}/{plantillaIdEnviado}/dias/1/{dia}"));
        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(esperado);

        var postBody = JsonNode.Parse(fakes.Handler.Requests[1].Cuerpo!)!;
        postBody["nombre"]!.GetValue<string>().Should().Be("Semana Tipo Cocina");
        postBody["semanas"]!.GetValue<int>().Should().Be(1);

        var putLunes = JsonNode.Parse(fakes.Handler.Requests[2].Cuerpo!)!;
        putLunes["turnoId"]!.GetValue<string>().Should().Be("8f14e45f-ceea-4b3c-8f0a-000000000001");
        var putMartes = JsonNode.Parse(fakes.Handler.Requests[3].Cuerpo!)!;
        putMartes["turnoId"]!.GetValue<string>().Should().Be("8f14e45f-ceea-4b3c-8f0a-000000000002");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(CrearPlantillaSemanalTool.Mensajes.ResultadoPlantillaCreada);
        json["plantilla"]!["id"]!.GetValue<string>().Should().Be(plantillaIdEnviado);
        json["plantilla"]!["nombre"]!.GetValue<string>().Should().Be("Semana Tipo Cocina");
        json["plantilla"]!["semanas"]!.GetValue<int>().Should().Be(1);
        json["diasAsignados"]!.GetValue<int>().Should().Be(7);
        json["completa"]!.GetValue<bool>().Should().BeTrue();
        json.AsObject().ContainsKey("diasRechazados").Should().BeFalse("filtro de relevancia: sin rechazos, la clave se omite");
        json["nota"]!.GetValue<string>().Should().Be(CrearPlantillaSemanalTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-3: "Miércoles", "miercoles" y 3 deben resolver al mismo dia ISO (3).
    [Fact]
    public async Task CrearPlantillaSemanal_TrataMiercolesConTildeSinTildeYNumerico_ComoElMismoDia()
    {
        async Task<string> RutaDelPut(string valorDia)
        {
            var fakes = CrearTool();
            var dias = $$"""[{"semana":1,"dia":{{JsonSerializer.Serialize(valorDia)}},"turno":"Cocina Manana"}]""";

            await fakes.Tool.Run(null!, "Plantilla X", 1, dias, TestContext.Current.CancellationToken);

            return fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Put).Ruta;
        }

        (await RutaDelPut("Miércoles")).Should().EndWith("/dias/1/3");
        (await RutaDelPut("miercoles")).Should().EndWith("/dias/1/3");
        (await RutaDelPut("3")).Should().EndWith("/dias/1/3");
    }

    // CA-2: se resuelven TODOS los nombres con una sola lectura; si alguno falta, no se escribe
    // nada -- ni POST ni PUT -- y se listan los que faltan y los disponibles.
    [Fact]
    public async Task CrearPlantillaSemanal_RespondeTurnosNoExistenSinEscribirNada_CuandoAlgunTurnoNoExisteEnElCatalogo()
    {
        var fakes = CrearTool();
        var dias = """
            [{"semana":1,"dia":"lunes","turno":"Turno Que No Existe"},{"semana":1,"dia":"martes","turno":"Cocina Tarde"}]
            """;

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1, dias, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            CrearPlantillaSemanalTool.Mensajes.TurnosNoExisten, "Turno Que No Existe", "Cocina Manana, Cocina Tarde"));
        fakes.Handler.Requests.Should().ContainSingle(r => r.Metodo == HttpMethod.Get);
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoElNombreEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "   ", 1, UnaEntradaValidaJson, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.CampoObligatorio, "nombre"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoDiasEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(null!, "Plantilla X", 1, "   ", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.CampoObligatorio, "dias"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoDiasNoEsJsonValido()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1, "esto no es json", TestContext.Current.CancellationToken);

        resultado.Should().Be(CrearPlantillaSemanalTool.Mensajes.DiasJsonInvalido);
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoDiasEsUnaListaVacia()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(null!, "Plantilla X", 1, "[]", TestContext.Current.CancellationToken);

        resultado.Should().Be(CrearPlantillaSemanalTool.Mensajes.DiasVacio);
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoElDiaEsUnNombreDesconocido()
    {
        var fakes = CrearTool();
        var dias = """[{"semana":1,"dia":"funes","turno":"Cocina Manana"}]""";

        var resultado = await fakes.Tool.Run(null!, "Plantilla X", 1, dias, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.DiaDesconocido, "funes"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoElDiaEsUnNumeroFueraDeRango()
    {
        var fakes = CrearTool();
        var dias = """[{"semana":1,"dia":8,"turno":"Cocina Manana"}]""";

        var resultado = await fakes.Tool.Run(null!, "Plantilla X", 1, dias, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.DiaDesconocido, "8"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoLaSemanaEstaFueraDeRango()
    {
        var fakes = CrearTool();
        var dias = """[{"semana":2,"dia":"lunes","turno":"Cocina Manana"}]""";

        var resultado = await fakes.Tool.Run(null!, "Plantilla X", 1, dias, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.SemanaFueraDeRango, 2, 1));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoHayDosEntradasParaLaMismaSemanaYDia()
    {
        var fakes = CrearTool();
        var dias = """
            [{"semana":1,"dia":"lunes","turno":"Cocina Manana"},{"semana":1,"dia":1,"turno":"Cocina Tarde"}]
            """;

        var resultado = await fakes.Tool.Run(null!, "Plantilla X", 1, dias, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.DiaDuplicado, 1, 1));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoSemanasEsCero()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 0, UnaEntradaValidaJson, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.SemanasFueraDeRango, 0));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoSemanasEsSiete()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 7, UnaEntradaValidaJson, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.SemanasFueraDeRango, 7));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    // CA-4: el 409 del POST (nombre duplicado) corta antes de cualquier PUT.
    [Fact]
    public async Task CrearPlantillaSemanal_TraduceElRechazoDelDominioSinPut_Cuando409DelPost()
    {
        const string cuerpo = "Ya existe una plantilla con el nombre 'Semana Tipo Cocina'";
        var fakes = CrearTool(statusPost: HttpStatusCode.Conflict, cuerpoPost: cuerpo);

        var resultado = await fakes.Tool.Run(
            null!, "Semana Tipo Cocina", 1, UnaEntradaValidaJson, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.RechazoDelDominio, cuerpo));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_TraduceElRechazoDelDominioSinPut_Cuando400DelPost()
    {
        var fixture = Validacion400Json;
        var fakes = CrearTool(statusPost: HttpStatusCode.BadRequest, cuerpoPost: fixture);

        var resultado = await fakes.Tool.Run(
            null!, "Semana Tipo Cocina", 1, UnaEntradaValidaJson, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.RechazoDelDominio, fixture));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    // CA-4: un PUT rechazado (409 turno incompleto) no detiene al resto -- 6 asignados, 1
    // rechazado, plantilla incompleta.
    [Fact]
    public async Task CrearPlantillaSemanal_DejaUnDiaRechazadoYAsignaLosDemas_CuandoUnPutFalla()
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, TurnosJson);
        handler.Responde(HttpMethod.Post, RutaPlantillas, HttpStatusCode.Created, "");

        const string motivoRechazo = "El turno esta incompleto";
        handler.RespondeConPrefijo(HttpMethod.Put, $"{RutaPlantillas}/", (request, _) =>
            request.RequestUri!.AbsolutePath.EndsWith("/dias/1/3", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Conflict)
                { Content = new StringContent(motivoRechazo, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NoContent));

        var tool = new CrearPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "Semana Tipo Cocina", 1, SieteDiasValidosJson, TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!;
        json["diasAsignados"]!.GetValue<int>().Should().Be(6);
        json["completa"]!.GetValue<bool>().Should().BeFalse();

        var rechazados = json["diasRechazados"]!.AsArray();
        rechazados.Should().HaveCount(1);
        var rechazado = rechazados.Single()!;
        rechazado["semana"]!.GetValue<int>().Should().Be(1);
        rechazado["dia"]!.GetValue<string>().Should().Be("miercoles");
        rechazado["turno"]!.GetValue<string>().Should().Be("Cocina Manana");
        rechazado["motivo"]!.GetValue<string>().Should().Be(motivoRechazo);

        handler.Requests.Where(r => r.Metodo == HttpMethod.Put).Should().HaveCount(7,
            "un PUT rechazado no detiene al resto del lote");
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoLaEntradaNoTraeLaClaveDia()
    {
        var fakes = CrearTool();
        var dias = """[{"semana":1,"turno":"Cocina Manana"}]""";

        var resultado = await fakes.Tool.Run(null!, "Plantilla X", 1, dias, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.DiaDesconocido, ""));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoLaEntradaNoTraeLaClaveTurno()
    {
        var fakes = CrearTool();
        var dias = """[{"semana":1,"dia":"lunes"}]""";

        var resultado = await fakes.Tool.Run(null!, "Plantilla X", 1, dias, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.TurnoOFranjaObligatorio, 1, "lunes"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    // Boundary del sistema: un 503 de un Function App frio no trae body -- el motivo cae al status
    // (RespuestaDelDominio), nunca a un mensaje vacio que el modelo no pueda interpretar.
    [Fact]
    public async Task CrearPlantillaSemanal_TraduceElStatusSinPut_CuandoElPostFallaSinCuerpo()
    {
        var fakes = CrearTool(statusPost: HttpStatusCode.ServiceUnavailable);

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1, UnaEntradaValidaJson, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    private const string IdHomonimo = "8f14e45f-ceea-4b3c-8f0a-0000000000a1";

    private static string FranjaJson(string inicio, string fin, int offsetFin, string descansos = "[]", string? sedeId = null) =>
        $$"""{"horaInicio":"{{inicio}}:00","horaFin":"{{fin}}:00","diaOffsetFin":{{offsetFin}},"descansos":{{descansos}},"extras":[],"sedeId":{{(sedeId is null ? "null" : $"\"{sedeId}\"")}},"nombreSede":null,"descripcion":""}""";

    private static string CatalogoCon(string nombre, string franjas, bool esDescanso = false) =>
        $$"""[{"id":"{{IdHomonimo}}","nombre":"{{nombre}}","esDescanso":{{(esDescanso ? "true" : "false")}},"horarioResumido":"","franjas":[{{franjas}}],"descripcion":"","completo":true}]""";

    private static string DiasInline(params (string Dia, string Franja)[] entradas) =>
        "[" + string.Join(",", entradas.Select(e => $$"""{"semana":1,"dia":"{{e.Dia}}","franja":"{{e.Franja}}"}""")) + "]";

    private static IEnumerable<JsonNode> PostsATurnos(Fakes fakes) =>
        fakes.Handler.Requests
            .Where(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaTurnos)
            .Select(r => JsonNode.Parse(r.Cuerpo!)!);

    private static List<(HttpMethod Metodo, string Ruta, string? Cuerpo)> AgregarFranjas(Fakes fakes) =>
        fakes.Handler.Requests
            .Where(r => r.Metodo == HttpMethod.Post && r.Ruta.EndsWith(":agregar-franja", StringComparison.Ordinal))
            .ToList();

    private static void NoEscribioNada(Fakes fakes) =>
        fakes.Handler.Requests.Should().OnlyContain(r => r.Metodo == HttpMethod.Get);

    // CA-1
    [Fact]
    public async Task CrearPlantillaSemanal_CreaTurnoInlineConNombreDerivadoYAsignaElDia_CuandoLaFranjaNoTieneHomonimo()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1, DiasInline(("lunes", "7:00-17:00")), TestContext.Current.CancellationToken);

        var turno = PostsATurnos(fakes).Single();
        turno["nombre"]!.GetValue<string>().Should().Be("07:00-17:00");
        turno["esDescanso"]!.GetValue<bool>().Should().BeFalse();
        var turnoId = turno["turnoId"]!.GetValue<string>();
        Guid.TryParse(turnoId, out _).Should().BeTrue();

        var franja = AgregarFranjas(fakes).Single();
        franja.Ruta.Should().Be($"{RutaTurnos}/{turnoId}:agregar-franja");
        var franjaBody = JsonNode.Parse(franja.Cuerpo!)!;
        franjaBody["inicio"]!.GetValue<string>().Should().Be("07:00");
        franjaBody["fin"]!.GetValue<string>().Should().Be("17:00");

        var plantillaId = ExtraerPlantillaIdEnviado(fakes);
        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, RutaTurnos),
            (HttpMethod.Post, RutaTurnos),
            (HttpMethod.Post, $"{RutaTurnos}/{turnoId}:agregar-franja"),
            (HttpMethod.Post, RutaPlantillas),
            (HttpMethod.Put, $"{RutaPlantillas}/{plantillaId}/dias/1/1"));
        JsonNode.Parse(fakes.Handler.Requests[4].Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(turnoId);

        var json = JsonNode.Parse(resultado)!;
        json["diasAsignados"]!.GetValue<int>().Should().Be(1);
        var inline = json["turnosInline"]!.AsArray().Single()!;
        inline["nombre"]!.GetValue<string>().Should().Be("07:00-17:00");
        inline["accion"]!.GetValue<string>().Should().Be("creo");
    }

    [Fact]
    public async Task CrearPlantillaSemanal_NombraConSufijoMasUno_CuandoLaFranjaCruzaMedianoche()
    {
        var fakes = CrearTool();

        await fakes.Tool.Run(
            null!, "Plantilla X", 1, DiasInline(("lunes", "22:00-06:00")), TestContext.Current.CancellationToken);

        PostsATurnos(fakes).Single()["nombre"]!.GetValue<string>().Should().Be("22:00-06:00+1");
        var franjaBody = JsonNode.Parse(AgregarFranjas(fakes).Single().Cuerpo!)!;
        franjaBody["inicio"]!.GetValue<string>().Should().Be("22:00");
        franjaBody["fin"]!.GetValue<string>().Should().Be("06:00");
    }

    [Fact]
    public async Task CrearPlantillaSemanal_EnviaDiaOffsetFinUno_CuandoLaFranjaEsDeVeinticuatroHoras()
    {
        var fakes = CrearTool();

        await fakes.Tool.Run(
            null!, "Plantilla X", 1, DiasInline(("lunes", "07:00-07:00")), TestContext.Current.CancellationToken);

        PostsATurnos(fakes).Single()["nombre"]!.GetValue<string>().Should().Be("07:00-07:00+1");
        var franjaBody = JsonNode.Parse(AgregarFranjas(fakes).Single().Cuerpo!)!;
        franjaBody["diaOffsetFin"]!.GetValue<int>().Should().Be(1);
    }

    // CA-2
    [Fact]
    public async Task CrearPlantillaSemanal_CreaUnSoloTurno_CuandoDosDiasTraenLaMismaFranjaEscritaDistinto()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1,
            DiasInline(("lunes", "7:00-17:00"), ("martes", "07:00-17:00")),
            TestContext.Current.CancellationToken);

        var turno = PostsATurnos(fakes).Single();
        var turnoId = turno["turnoId"]!.GetValue<string>();
        AgregarFranjas(fakes).Should().ContainSingle();
        var puts = fakes.Handler.Requests.Where(r => r.Metodo == HttpMethod.Put).ToList();
        puts.Should().HaveCount(2);
        puts.Should().OnlyContain(p => JsonNode.Parse(p.Cuerpo!)!["turnoId"]!.GetValue<string>() == turnoId);
        JsonNode.Parse(resultado)!["turnosInline"]!.AsArray().Should().ContainSingle();
    }

    // CA-3
    [Fact]
    public async Task CrearPlantillaSemanal_ReutilizaElTurno_CuandoElHomonimoEsEquivalente()
    {
        var fakes = CrearTool(turnosJson: CatalogoCon("07:00-17:00", FranjaJson("07:00", "17:00", 0)));

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1, DiasInline(("lunes", "7:00-17:00")), TestContext.Current.CancellationToken);

        PostsATurnos(fakes).Should().BeEmpty();
        AgregarFranjas(fakes).Should().BeEmpty();
        var put = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        JsonNode.Parse(put.Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(IdHomonimo);
        JsonNode.Parse(resultado)!["turnosInline"]!.AsArray().Single()!["accion"]!.GetValue<string>().Should().Be("reutilizo");
    }

    // CA-4
    [Fact]
    public async Task CrearPlantillaSemanal_AgregaLaFranjaAlHomonimoVacio_CuandoElTurnoNoTieneFranjas()
    {
        var fakes = CrearTool(turnosJson: CatalogoCon("07:00-17:00", ""));

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1, DiasInline(("lunes", "07:00-17:00")), TestContext.Current.CancellationToken);

        PostsATurnos(fakes).Should().BeEmpty();
        AgregarFranjas(fakes).Single().Ruta.Should().Be($"{RutaTurnos}/{IdHomonimo}:agregar-franja");
        var put = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        JsonNode.Parse(put.Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(IdHomonimo);
        JsonNode.Parse(resultado)!["turnosInline"]!.AsArray().Single()!["accion"]!.GetValue<string>().Should().Be("completo");
    }

    // CA-5
    private static async Task AssertConflictoSinEscribir(string catalogo)
    {
        var fakes = CrearTool(turnosJson: catalogo);

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1, DiasInline(("lunes", "07:00-17:00")), TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.TurnoInlineEnConflicto, "07:00-17:00"));
        NoEscribioNada(fakes);
    }

    [Fact]
    public Task CrearPlantillaSemanal_AbortaSinEscribir_CuandoElHomonimoTieneOtraFranja() =>
        AssertConflictoSinEscribir(CatalogoCon("07:00-17:00", FranjaJson("08:00", "18:00", 0)));

    [Fact]
    public Task CrearPlantillaSemanal_AbortaSinEscribir_CuandoElHomonimoTieneDescansos() =>
        AssertConflictoSinEscribir(CatalogoCon(
            "07:00-17:00",
            FranjaJson("07:00", "17:00", 0, descansos: """[{"horaInicio":"12:00:00","horaFin":"13:00:00","diaOffsetInicio":0,"diaOffsetFin":0}]""")));

    [Fact]
    public Task CrearPlantillaSemanal_AbortaSinEscribir_CuandoElHomonimoTieneSede() =>
        AssertConflictoSinEscribir(CatalogoCon(
            "07:00-17:00", FranjaJson("07:00", "17:00", 0, sedeId: "8f14e45f-ceea-4b3c-8f0a-0000000000b1")));

    [Fact]
    public Task CrearPlantillaSemanal_AbortaSinEscribir_CuandoElHomonimoEsDescanso() =>
        AssertConflictoSinEscribir(CatalogoCon("07:00-17:00", "", esDescanso: true));

    // CA-6
    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoLaFranjaTieneFormatoInvalido()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1, DiasInline(("lunes", "25:00-17:00")), TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.FranjaInvalida, 1, "lunes", "25:00-17:00"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoLaEntradaTraeTurnoYFranja()
    {
        var fakes = CrearTool();
        var dias = """[{"semana":1,"dia":"lunes","turno":"Cocina Manana","franja":"07:00-17:00"}]""";

        var resultado = await fakes.Tool.Run(null!, "Plantilla X", 1, dias, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.TurnoYFranjaExcluyentes, 1, "lunes"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_AbortaAntesDeLaPlantilla_CuandoCrearTurnoResponde409()
    {
        var fakes = CrearTool(statusCrearTurno: HttpStatusCode.Conflict);

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1, DiasInline(("lunes", "07:00-17:00")), TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            CrearPlantillaSemanalTool.Mensajes.TurnoInlineNombreDuplicado,
            "07:00-17:00",
            CrearPlantillaSemanalTool.Mensajes.NingunTurnoInlineCreado));
        fakes.Handler.Requests.Should().NotContain(r => r.Ruta == RutaPlantillas);
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_AbortaAntesDeLaPlantillaEInformaLosCreados_CuandoAgregarFranjaFalla()
    {
        var fakes = CrearTool(
            statusAgregarFranja: cuerpo => cuerpo!.Contains("\"inicio\":\"09:00\"")
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.NoContent);

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla X", 1,
            DiasInline(("lunes", "07:00-17:00"), ("martes", "09:00-18:00")),
            TestContext.Current.CancellationToken);

        resultado.Should().Contain("07:00-17:00").And.Contain("09:00-18:00").And.Contain("503");
        fakes.Handler.Requests.Should().NotContain(r => r.Ruta == RutaPlantillas);
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }
}
