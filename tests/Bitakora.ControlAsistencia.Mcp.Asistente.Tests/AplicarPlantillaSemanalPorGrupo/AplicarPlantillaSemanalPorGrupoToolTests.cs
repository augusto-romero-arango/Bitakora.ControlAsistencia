using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanalPorGrupo;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AplicarPlantillaSemanalPorGrupo;

public class AplicarPlantillaSemanalPorGrupoToolTests
{
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes";
    private const string TurnoA = "8f14e45f-ceea-4b3c-8f0a-000000000001";
    private const string TurnoB = "8f14e45f-ceea-4b3c-8f0a-000000000002";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static object Slot(int semana, int dia, string id, bool completo = true, bool retirado = false) => new
    {
        semana,
        dia,
        turno = new { id, nombre = $"Turno {id[^1]}", descripcion = "(06:00-14:00)", completo, retirado }
    };

    private static string Plantillas(bool completa = true, bool turnoBRetirado = false) => JsonSerializer.Serialize(
        new[]
        {
            new
            {
                id = "01a07000-1000-7000-9000-000000000001",
                nombre = "Semana A",
                semanas = 2,
                completa,
                dias = new[]
                {
                    Slot(1, 1, TurnoA), Slot(1, 2, TurnoA), Slot(1, 3, TurnoA), Slot(1, 4, TurnoA),
                    Slot(1, 5, TurnoA), Slot(1, 6, TurnoA), Slot(1, 7, TurnoA),
                    Slot(2, 1, TurnoB, true, turnoBRetirado), Slot(2, 2, TurnoB, true, turnoBRetirado),
                    Slot(2, 3, TurnoB, true, turnoBRetirado), Slot(2, 4, TurnoB, true, turnoBRetirado),
                    Slot(2, 5, TurnoB, true, turnoBRetirado), Slot(2, 6, TurnoB, true, turnoBRetirado),
                    Slot(2, 7, TurnoB, true, turnoBRetirado)
                }
            }
        },
        Web);

    private static string Turnos(string? sedeIdDeLaFranja) => JsonSerializer.Serialize(
        new[] { TurnoA, TurnoB }.Select(id => new
        {
            id,
            nombre = $"Turno {id[^1]}",
            esDescanso = false,
            horarioResumido = "06:00-14:00",
            franjas = new[]
            {
                new
                {
                    horaInicio = "06:00:00", horaFin = "14:00:00", diaOffsetFin = 0,
                    descansos = Array.Empty<object>(), extras = Array.Empty<object>(),
                    sedeId = sedeIdDeLaFranja, nombreSede = (string?)null, descripcion = ""
                }
            },
            descripcion = "",
            completo = true
        }),
        Web);

    private static object Ficha(int numero, string vigenteDesde = "2025-01-01", string? codigoSede = "SUBA") => new
    {
        id = $"CC-{numero:D4}",
        nombreCompleto = $"Colab {numero:D4}",
        codigoColaborador = $"C{numero:D4}",
        vigenteDesde,
        vigenteHasta = (string?)null,
        etiquetas = Array.Empty<object>(),
        codigoSede
    };

    private static List<object> Fichas(int cantidad) => [.. Enumerable.Range(1, cantidad).Select(n => Ficha(n))];

    private sealed record Entorno(
        AplicarPlantillaSemanalPorGrupoTool Tool, HandlerPorRuta Programacion, List<JsonNode> ConsultasDeFichas)
    {
        public List<JsonNode> Posts =>
            [.. Programacion.Requests
                .Where(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaSolicitudes)
                .Select(r => JsonNode.Parse(r.Cuerpo!)!)];

        public void SinEscrituras()
        {
            Posts.Should().BeEmpty();
            ConsultasDeFichas.Should().BeEmpty();
        }
    }

    private static Entorno Crear(
        IReadOnlyList<object>? fichas = null, string? plantillasJson = null, string? sedeDeLaFranja = "SUBA",
        Func<string?, HttpResponseMessage>? respuestaPost = null,
        ILogger<AplicarPlantillaSemanalPorGrupoTool>? logger = null)
    {
        var consultas = new List<JsonNode>();
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaPlantillas, HttpStatusCode.OK, plantillasJson ?? Plantillas());
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, Turnos(sedeDeLaFranja));
        programacion.Responde(
            HttpMethod.Post, RutaSolicitudes,
            (_, cuerpo) => respuestaPost?.Invoke(cuerpo) ?? EntornoDeSedes.Respuesta(HttpStatusCode.Created, ""));
        var (clienteSedes, _) = ClienteFalso.ConFuncion(r => EntornoDeSedes.RespondeSedes(r));
        var (clienteColaboradores, _) = ClienteFalso.ConFuncion(request =>
        {
            var cuerpo = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            lock (consultas)
                consultas.Add(JsonNode.Parse(cuerpo)!);
            return EntornoDeSedes.Respuesta(HttpStatusCode.OK, JsonSerializer.Serialize(fichas ?? [], Web));
        });

        var tool = new AplicarPlantillaSemanalPorGrupoTool(
            new ProgramacionApi(clienteProgramacion), new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores), logger);
        return new Entorno(tool, programacion, consultas);
    }

    private static Task<string> Ejecutar(
        Entorno entorno, string desde = "2026-09-01", string hasta = "2026-09-14", string plantilla = "Semana A",
        string? sedeDeProgramacion = null, string? sede = null, string? etiquetas = "area:cocina") =>
        entorno.Tool.Run(
            null!, desde, hasta, plantilla, sedeDeProgramacion, sede, etiquetas,
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaLaEtiqueta_CuandoTambienFaltaLaPlantilla()
    {
        var entorno = Crear(plantillasJson: "[]");

        var resultado = await Ejecutar(entorno, plantilla: "[TEST] Plantilla que no existe", etiquetas: "area");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.EtiquetaMalFormada, "area"));
        entorno.Programacion.Requests.Should().BeEmpty();
        entorno.ConsultasDeFichas.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaElSelectorObligatorio_CuandoTambienFaltaLaPlantilla()
    {
        var entorno = Crear(plantillasJson: "[]");

        var resultado = await Ejecutar(
            entorno, plantilla: "[TEST] Plantilla que no existe", sede: null, etiquetas: null);

        resultado.Should().Be(AplicarPlantillaSemanalPorGrupoTool.Mensajes.SelectorObligatorio);
        entorno.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoNoLlegaSedeNiEtiquetas()
    {
        var entorno = Crear();

        var resultado = await Ejecutar(entorno, sede: null, etiquetas: null);

        resultado.Should().Be(AplicarPlantillaSemanalPorGrupoTool.Mensajes.SelectorObligatorio);
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoSedeYEtiquetasVienenEnBlanco()
    {
        var entorno = Crear();

        var resultado = await Ejecutar(entorno, sede: "  ", etiquetas: " ");

        resultado.Should().Be(AplicarPlantillaSemanalPorGrupoTool.Mensajes.SelectorObligatorio);
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaNombrandoElPar_CuandoUnaEtiquetaNoTieneDosPuntos()
    {
        var entorno = Crear();

        var resultado = await Ejecutar(entorno, etiquetas: "area:cocina, area");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.EtiquetaMalFormada, "area"));
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaSedeDelSelectorNoExiste()
    {
        var entorno = Crear();

        var resultado = await Ejecutar(entorno, sede: "FANTASMA", etiquetas: null);

        resultado.Should().Be(string.Format(
            AplicarPlantillaSemanalPorGrupoTool.Mensajes.SedeDelSelectorNoExiste, "FANTASMA"));
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaSedeDelSelectorEstaInactiva()
    {
        var entorno = Crear();

        var resultado = await Ejecutar(entorno, sede: "NORTE", etiquetas: null);

        resultado.Should().Be(string.Format(
            AplicarPlantillaSemanalPorGrupoTool.Mensajes.SedeDelSelectorInactiva, "NORTE"));
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaPlantillaNoExiste()
    {
        var entorno = Crear();

        var resultado = await Ejecutar(entorno, plantilla: "Inexistente");

        resultado.Should().Contain("Inexistente").And.Contain("Semana A");
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaPlantillaEstaIncompleta()
    {
        var entorno = Crear(plantillasJson: Plantillas(completa: false));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.PlantillaIncompleta, "Semana A"));
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaPlantillaReferenciaUnTurnoRetirado()
    {
        var entorno = Crear(plantillasJson: Plantillas(turnoBRetirado: true));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(
            AplicarPlantillaSemanalPorGrupoTool.Mensajes.PlantillaConTurnoNoProgramable, "Semana A"));
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaVentanaSupera35Dias()
    {
        var entorno = Crear();

        var resultado = await Ejecutar(entorno, desde: "2026-09-01", hasta: "2026-10-06");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.VentanaExcedeMaximo, 36));
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaSedeDeProgramacionNoExiste()
    {
        var entorno = Crear();

        var resultado = await Ejecutar(entorno, sedeDeProgramacion: "FANTASMA");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.SedeNoExiste, "FANTASMA"));
        entorno.SinEscrituras();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_ProgramaATodosConUnaSolaLlamadaAFichas_Cuando203Fichas()
    {
        var entorno = Crear(Fichas(203));

        var resultado = await Ejecutar(entorno, desde: "2026-09-02", hasta: "2026-09-14");

        entorno.ConsultasDeFichas.Should().ContainSingle();
        entorno.ConsultasDeFichas[0].AsObject().ContainsKey("take").Should().BeFalse("la llamada interna no pagina");
        entorno.ConsultasDeFichas[0].AsObject().ContainsKey("cursor").Should().BeFalse();
        entorno.ConsultasDeFichas[0]["fechaReferencia"]!.GetValue<string>().Should().Be("2026-09-02");
        entorno.Posts.Should().HaveCount(406, "dos turnos distintos por colaborador");
        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().HaveCount(203);
        json["grupoResuelto"]!.GetValue<int>().Should().Be(203);
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_EnviaSedeCanonicaYEtiquetasJuntas_CuandoVienenAmbas()
    {
        var entorno = Crear(Fichas(1));

        await Ejecutar(entorno, sede: "SUBA", etiquetas: "area:cocina, Turno:Noche");

        var consulta = entorno.ConsultasDeFichas.Single();
        consulta["codigoSede"]!.GetValue<string>().Should().Be("SUBA");
        var etiquetas = consulta["etiquetas"]!.AsArray();
        etiquetas.Should().HaveCount(2);
        etiquetas[0]!["categoria"]!.GetValue<string>().Should().Be("area");
        etiquetas[0]!["valor"]!.GetValue<string>().Should().Be("cocina");
        etiquetas[1]!["categoria"]!.GetValue<string>().Should().Be("Turno");
        etiquetas[1]!["valor"]!.GetValue<string>().Should().Be("Noche");
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_NoEnviaCodigoDeSede_CuandoSoloLlegaronEtiquetas()
    {
        var entorno = Crear(Fichas(1));

        await Ejecutar(entorno, sede: null, etiquetas: "area:cocina");

        var consulta = entorno.ConsultasDeFichas.Single();
        consulta["codigoSede"].Should().BeNull();
        consulta["etiquetas"]!.AsArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_EnviaUnPostPorTurnoDistintoConSusFechas_CuandoElMoldeAlternaSemanas()
    {
        var entorno = Crear(Fichas(1));

        await Ejecutar(entorno, desde: "2026-09-02", hasta: "2026-09-14");

        entorno.Posts.Should().HaveCount(2);
        string[] Fechas(string turnoId) => [.. entorno.Posts
            .Single(p => p["turnoId"]!.GetValue<string>() == turnoId)["fechas"]!.AsArray()
            .Select(f => f!.GetValue<string>())];
        Fechas(TurnoA).Should().Equal(
            "2026-09-02", "2026-09-03", "2026-09-04", "2026-09-05", "2026-09-06", "2026-09-14");
        Fechas(TurnoB).Should().Equal(
            "2026-09-07", "2026-09-08", "2026-09-09", "2026-09-10", "2026-09-11", "2026-09-12", "2026-09-13");
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_ReportaPlantillaSelectorYGrupoResuelto_CuandoProgramaElGrupo()
    {
        var entorno = Crear(Fichas(2));

        var resultado = await Ejecutar(entorno, sede: "SUBA", etiquetas: "area:cocina");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>()
            .Should().Be(AplicarPlantillaSemanalPorGrupoTool.Mensajes.ResultadoPlantillaAplicada);
        json["plantilla"]!.GetValue<string>().Should().Be("Semana A");
        json["ventana"]!.GetValue<string>().Should().Be("2026-09-01 a 2026-09-14");
        json["selector"]!.GetValue<string>().Should().Contain("SUBA").And.Contain("area:cocina");
        json["grupoResuelto"]!.GetValue<int>().Should().Be(2);
        json["omitidos"]!.GetValue<int>().Should().Be(0);
        json["programados"]!.AsArray().Should().HaveCount(2);
        json.AsObject().ContainsKey("fallidos").Should().BeFalse();
        json["nota"]!.GetValue<string>()
            .Should().Be(AplicarPlantillaSemanalPorGrupoTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_EmiteElIndicadorConModalidadPlantillaGrupo_CuandoLlegaALaFaseDeEjecucion()
    {
        var logger = new LoggerDeCaptura<AplicarPlantillaSemanalPorGrupoTool>();
        var entorno = Crear(Fichas(2), logger: logger);

        await Ejecutar(entorno, sede: "SUBA", etiquetas: "area:cocina, Turno:Noche");

        var registro = logger.Registros.Where(r => r.EventId.Name == "EjecucionSolicitudProgramacion")
            .Should().ContainSingle().Subject;
        registro.Propiedades["Modalidad"].Should().Be("plantilla-grupo");
        registro.Propiedades["TamanoResuelto"].Should().Be(2);
        registro.Propiedades["Programados"].Should().Be(2);
        registro.Propiedades["Turno"].Should().Be("Semana A");
        registro.Propiedades["Sede"].Should().Be("SUBA");
        registro.Propiedades["Etiquetas"].Should().Be("area:cocina, Turno:Noche");
        registro.Propiedades.GetValueOrDefault("SedeDeProgramacion").Should().BeNull();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_ReportaFallidoConElTurnoYProgramaElResto_CuandoUnPostEsRechazado()
    {
        var entorno = Crear(
            Fichas(2),
            respuestaPost: cuerpo => cuerpo!.Contains("C0002") && cuerpo.Contains(TurnoB)
                ? EntornoDeSedes.Respuesta(HttpStatusCode.Conflict, "turno rechazado")
                : EntornoDeSedes.Respuesta(HttpStatusCode.Created, ""));

        var resultado = await Ejecutar(entorno);

        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().HaveCount(2);
        var fallido = json["fallidos"]!.AsArray().Single()!;
        fallido["identificacion"]!.GetValue<string>().Should().Be("CC-0002");
        fallido["turno"]!.GetValue<string>().Should().Be("Turno 2");
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_AvisaSinSede_CuandoElColaboradorNoTieneSedeYUnTurnoTieneFranjaSinSede()
    {
        var entorno = Crear([Ficha(1, codigoSede: null)], sedeDeLaFranja: null);

        var resultado = await Ejecutar(entorno);

        JsonNode.Parse(resultado)!["avisos"]!.AsArray().Single()!["identificacion"]!
            .GetValue<string>().Should().Be("CC-0001");
        entorno.Programacion.Requests.Count(r => r.Metodo == HttpMethod.Get && r.Ruta == RutaTurnos)
            .Should().Be(1);
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_ReportaCeroProgramadosSinRechazo_CuandoElGrupoEstaVacio()
    {
        var entorno = Crear([]);

        var resultado = await Ejecutar(entorno);

        var json = JsonNode.Parse(resultado)!;
        json["grupoResuelto"]!.GetValue<int>().Should().Be(0);
        json["programados"]!.AsArray().Should().BeEmpty();
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaConElDominio_CuandoLaConsultaDeFichasFalla()
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaPlantillas, HttpStatusCode.OK, Plantillas());
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, Turnos("SUBA"));
        var (clienteSedes, _) = ClienteFalso.ConFuncion(r => EntornoDeSedes.RespondeSedes(r));
        var (clienteColaboradores, _) = ClienteFalso.Con("boom", HttpStatusCode.InternalServerError);
        var tool = new AplicarPlantillaSemanalPorGrupoTool(
            new ProgramacionApi(clienteProgramacion), new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores));

        var resultado = await tool.Run(
            null!, "2026-09-01", "2026-09-14", "Semana A", null, null, "area:cocina",
            TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.RechazoDelDominio, "boom"));
        programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }
}
