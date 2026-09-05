using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.CrearPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.CrearPlantillaSemanal;

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
        HttpStatusCode statusPut = HttpStatusCode.NoContent)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaTurnos, statusTurnos, turnosJson ?? TurnosJson);
        if (statusPost is { } status)
            handler.Responde(HttpMethod.Post, RutaPlantillas, status, cuerpoPost);
        handler.RespondeConPrefijo(HttpMethod.Put, $"{RutaPlantillas}/", statusPut, "");

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
}
