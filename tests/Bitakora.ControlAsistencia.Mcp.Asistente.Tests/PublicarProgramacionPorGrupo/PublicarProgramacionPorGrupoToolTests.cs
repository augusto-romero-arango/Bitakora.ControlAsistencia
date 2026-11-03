using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.PublicarProgramacionPorGrupo;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.PublicarProgramacionPorGrupo;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.PublicarProgramacionPorGrupo;

public class PublicarProgramacionPorGrupoToolTests
{
    private const string Verbo = "publicar";
    private static readonly IReadOnlyList<object> DosFichas = [ColaboradoresFalso.Ficha(1), ColaboradoresFalso.Ficha(2)];

    private static PublicarProgramacionPorGrupoTool CrearTool(EntornoDeActoPorGrupo e) =>
        new(e.Api, e.ResolutorSedes, e.ResolutorCandidatos, EntornoDeActoPorGrupo.RelojDeNoche);

    private static Task<string> Ejecutar(
        EntornoDeActoPorGrupo e, string? sede = "SUBA", string? etiquetas = null, bool? todo = null,
        string? desde = null, string? hasta = null) =>
        CrearTool(e).Run(null!, sede, etiquetas, todo, desde, hasta, TestContext.Current.CancellationToken);

    private static string CuerpoDe(string? cuerpo, string campo) => JsonNode.Parse(cuerpo!)![campo]!.GetValue<string>();

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_AplicaElActoACadaMiembroConElRangoSinLeerLaVista_CuandoElGrupoTieneRango()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, desde: "2026-09-01", hasta: "2026-09-03");

        var consulta = e.Colaboradores.Consultas.Single();
        consulta.Cuerpo["fechaReferencia"]!.GetValue<string>().Should().Be("2026-09-01");
        consulta.Cuerpo["codigoSede"]!.GetValue<string>().Should().Be("SUBA");
        e.LeyoLaVista.Should().BeFalse();
        var actos = e.Actos(Verbo);
        actos.Select(a => a.Ruta).Should().BeEquivalentTo(
            [EntornoDeActoPorGrupo.RutaActo("C1", Verbo), EntornoDeActoPorGrupo.RutaActo("C2", Verbo)]);
        actos.Should().OnlyContain(a => CuerpoDe(a.Cuerpo, "desde") == "2026-09-01" && CuerpoDe(a.Cuerpo, "hasta") == "2026-09-03");
        var json = JsonNode.Parse(resultado)!.AsObject();
        json["resultado"]!.GetValue<string>().Should().Be(PublicarProgramacionPorGrupoTool.Mensajes.ResultadoProgramacionPublicada);
        json["colaboradores"]!.GetValue<int>().Should().Be(2);
        json["alcance"]!.GetValue<string>().Should().Be("sede:SUBA");
        json["rango"]!.GetValue<string>().Should().Be("2026-09-01 a 2026-09-03");
        json.ContainsKey("fallidos").Should().BeFalse();
        json["nota"]!.GetValue<string>().Should().Be(PublicarProgramacionPorGrupoTool.Mensajes.Nota);
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_LeeLaVistaFiltradaYAplicaElActoSoloAQuienTieneBorrador_CuandoNoHayRango()
    {
        var vista = EntornoDeActoPorGrupo.Vista(("C1", "Colab 1", ["2026-11-03", "2026-10-28", "2026-10-30"]));
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas, vista);

        var resultado = await Ejecutar(e);

        var consultaFichas = e.Colaboradores.Consultas.Single();
        consultaFichas.Cuerpo["fechaReferencia"]!.GetValue<string>().Should().Be("2026-11-03");
        var lectura = e.Programacion.Requests.Single(r => r.Metodo == EntornoDeActoPorGrupo.Query);
        var cuerpoVista = JsonNode.Parse(lectura.Cuerpo!)!;
        cuerpoVista["fechaReferencia"]!.GetValue<string>().Should().Be("2026-11-03");
        cuerpoVista["colaboradores"]!.AsArray().Select(n => n!.GetValue<string>()).Should().BeEquivalentTo(["C1", "C2"]);
        var acto = e.Actos(Verbo).Single();
        acto.Ruta.Should().Be(EntornoDeActoPorGrupo.RutaActo("C1", Verbo));
        CuerpoDe(acto.Cuerpo, "desde").Should().Be("2026-10-28");
        CuerpoDe(acto.Cuerpo, "hasta").Should().Be("2026-11-03");
        var json = JsonNode.Parse(resultado)!.AsObject();
        json["colaboradores"]!.GetValue<int>().Should().Be(1);
        json["rango"]!.GetValue<string>().Should().Be(PublicarProgramacionPorGrupoTool.Mensajes.SinRango);
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_HaceUnaLlamadaPorTramoDe366Dias_CuandoLosDiasPendientesPasanDe366()
    {
        var vista = EntornoDeActoPorGrupo.Vista(("C1", "Colab 1", ["2026-01-01", "2027-03-01"]));
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas, vista);

        await Ejecutar(e);

        var actos = e.Actos(Verbo);
        actos.Should().HaveCount(2);
        actos.Select(a => (CuerpoDe(a.Cuerpo, "desde"), CuerpoDe(a.Cuerpo, "hasta"))).Should().BeEquivalentTo(
            [("2026-01-01", "2027-01-01"), ("2027-01-02", "2027-03-01")]);
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_LeeLaVistaSinFiltroYAplicaElActoACadaColaborador_CuandoEsTodoLoPendiente()
    {
        var vista = EntornoDeActoPorGrupo.Vista(
            ("AR01", "Ana Ruiz", ["2026-09-02", "2026-09-10"]), ("AR02", "Beto Diaz", ["2026-09-05"]));
        var e = EntornoDeActoPorGrupo.Crear(Verbo, [], vista);

        var resultado = await Ejecutar(e, sede: null, todo: true);

        e.Colaboradores.Consultas.Should().BeEmpty();
        var lectura = e.Programacion.Requests.Single(r => r.Metodo == EntornoDeActoPorGrupo.Query);
        JsonNode.Parse(lectura.Cuerpo!)!.AsObject().ContainsKey("colaboradores").Should().BeFalse();
        e.Actos(Verbo).Select(a => a.Ruta).Should().BeEquivalentTo(
            [EntornoDeActoPorGrupo.RutaActo("AR01", Verbo), EntornoDeActoPorGrupo.RutaActo("AR02", Verbo)]);
        var json = JsonNode.Parse(resultado)!.AsObject();
        json["colaboradores"]!.GetValue<int>().Should().Be(2);
        json["alcance"]!.GetValue<string>().Should().Be(PublicarProgramacionPorGrupoTool.Mensajes.AlcanceTodoLoPendiente);
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_PasaElRangoDadoAlActoYALaVista_CuandoEsTodoLoPendienteConRango()
    {
        var vista = EntornoDeActoPorGrupo.Vista(("AR01", "Ana Ruiz", ["2026-09-02"]));
        var e = EntornoDeActoPorGrupo.Crear(Verbo, [], vista);

        await Ejecutar(e, sede: null, todo: true, desde: "2026-09-01", hasta: "2026-09-30");

        var lectura = e.Programacion.Requests.Single(r => r.Metodo == EntornoDeActoPorGrupo.Query);
        CuerpoDe(lectura.Cuerpo, "desde").Should().Be("2026-09-01");
        CuerpoDe(lectura.Cuerpo, "hasta").Should().Be("2026-09-30");
        var acto = e.Actos(Verbo).Single();
        CuerpoDe(acto.Cuerpo, "desde").Should().Be("2026-09-01");
        CuerpoDe(acto.Cuerpo, "hasta").Should().Be("2026-09-30");
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_SigueConLosDemasYReportaFallidoCompleto_CuandoElActoFallaParaUnColaborador()
    {
        var e = EntornoDeActoPorGrupo.Crear(
            Verbo, DosFichas,
            respuestaActo: codigo => codigo == "C2"
                ? ColaboradoresFalso.Json(HttpStatusCode.Conflict, "El mes esta cerrado")
                : new HttpResponseMessage(HttpStatusCode.NoContent));

        var resultado = await Ejecutar(e, desde: "2026-09-01", hasta: "2026-09-03");

        e.Actos(Verbo).Should().HaveCount(2);
        var json = JsonNode.Parse(resultado)!.AsObject();
        json["colaboradores"]!.GetValue<int>().Should().Be(1);
        var fallido = json["fallidos"]!.AsArray().Single()!;
        fallido["codigoColaborador"]!.GetValue<string>().Should().Be("C2");
        fallido["nombreCompleto"]!.GetValue<string>().Should().Be("Colab 2");
        fallido["motivo"]!.GetValue<string>().Should().Contain("El mes esta cerrado");
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RespondeElRechazoSinLlamarAlActo_CuandoSinAlcance()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, null, null, null, null, null);

        resultado.Should().Be(PublicarProgramacionPorGrupoTool.Mensajes.AlcanceObligatorio);
        e.Actos(Verbo).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RespondeElRechazoSinLlamarAlActo_CuandoSelectorYTodoLoPendiente()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, "SUBA", null, true, null, null);

        resultado.Should().Be(PublicarProgramacionPorGrupoTool.Mensajes.AlcanceExcluyente);
        e.Actos(Verbo).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RespondeElRechazoSinLlamarAlActo_CuandoSoloDesde()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, "SUBA", null, null, "2026-09-01", null);

        resultado.Should().Be(PublicarProgramacionPorGrupoTool.Mensajes.RangoIncompleto);
        e.Actos(Verbo).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RespondeElRechazoSinLlamarAlActo_CuandoSoloHasta()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, "SUBA", null, null, null, "2026-09-03");

        resultado.Should().Be(PublicarProgramacionPorGrupoTool.Mensajes.RangoIncompleto);
        e.Actos(Verbo).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RespondeElRechazoSinLlamarAlActo_CuandoDesdeMayorQueHasta()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, "SUBA", null, null, "2026-09-03", "2026-09-01");

        resultado.Should().Be(PublicarProgramacionPorGrupoTool.Mensajes.RangoInvertido);
        e.Actos(Verbo).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RespondeElRechazoSinLlamarAlActo_CuandoRangoDe367Dias()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, "SUBA", null, null, "2026-01-01", "2027-01-02");

        resultado.Should().Be(string.Format(PublicarProgramacionPorGrupoTool.Mensajes.RangoExcedeElMaximo, 367));
        e.Actos(Verbo).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RechazaLaFechaMalFormada_CuandoNoEsYyyyMmDd()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, desde: "01/09/2026", hasta: "2026-09-03");

        resultado.Should().Be(string.Format(PublicarProgramacionPorGrupoTool.Mensajes.FechaInvalida, "desde", "01/09/2026"));
        e.Actos(Verbo).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RechazaLaEtiquetaMalFormada_SinLlamarAlActo()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, sede: null, etiquetas: "sinvalor", desde: "2026-09-01", hasta: "2026-09-03");

        resultado.Should().Be(string.Format(PublicarProgramacionPorGrupoTool.Mensajes.EtiquetaMalFormada, "sinvalor"));
        e.Actos(Verbo).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RechazaLaSedeInexistente_SinLlamarAlActo()
    {
        var e = EntornoDeActoPorGrupo.Crear(Verbo, DosFichas);

        var resultado = await Ejecutar(e, sede: "NOEXISTE", desde: "2026-09-01", hasta: "2026-09-03");

        resultado.Should().Be(string.Format(PublicarProgramacionPorGrupoTool.Mensajes.SedeDelSelectorNoExiste, "NOEXISTE"));
        e.Actos(Verbo).Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacionPorGrupoTool_RespondeElRechazoDelDominioSinAplicarActos_CuandoFallaLaVista()
    {
        var e = EntornoDeActoPorGrupo.Crear(
            Verbo, DosFichas, "vista caida", HttpStatusCode.InternalServerError);

        var resultado = await Ejecutar(e);

        resultado.Should().StartWith(string.Format(PublicarProgramacionPorGrupoTool.Mensajes.RechazoDelDominio, "").TrimEnd());
        e.Actos(Verbo).Should().BeEmpty();
    }
}
