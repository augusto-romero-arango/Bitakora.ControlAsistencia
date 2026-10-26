using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.RegistrarColaboradores;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.RegistrarColaboradores;

public class RegistrarColaboradoresToolTests
{
    private static JsonObject Fila(
        string numero, string? fechaInicio = "2026-09-01", string? primerNombre = "Ana",
        string tipo = "CC", string? codigoSede = null)
    {
        var fila = new JsonObject
        {
            ["tipo_identificacion"] = tipo,
            ["numero_identificacion"] = numero,
            ["primer_nombre"] = primerNombre,
            ["primer_apellido"] = "Perez",
            ["codigo_colaborador"] = $"C{numero}"
        };
        if (fechaInicio is not null)
            fila["fecha_inicio"] = fechaInicio;
        if (codigoSede is not null)
            fila["codigo_sede"] = codigoSede;
        return fila;
    }

    private static string Lista(params JsonObject[] filas) => new JsonArray([.. filas]).ToJsonString();

    private static Task<string> Ejecutar(HttpClient cliente, string colaboradores) =>
        new RegistrarColaboradoresTool(new ColaboradoresApi(cliente))
            .Run(context: null!, colaboradores: colaboradores, ct: TestContext.Current.CancellationToken);

    private static (HttpClient Cliente, HandlerPorRuta Handler) ClienteConStatusPorNumero(
        Func<string, HttpStatusCode> statusDe, string cuerpoRechazo = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Post, "/api/colaboradores", (_, cuerpo) =>
        {
            var numero = JsonNode.Parse(cuerpo!)!["numeroIdentificacion"]!.GetValue<string>();
            var status = statusDe(numero);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.Created ? string.Empty : cuerpoRechazo)
            };
        });
        return (cliente, handler);
    }

    [Fact]
    public async Task RegistrarColaboradores_EnviaUnPostPorFilaYCuentaLosRegistrados_Cuando201()
    {
        var (cliente, handler) = ClienteConStatusPorNumero(_ => HttpStatusCode.Created);

        var resultado = await Ejecutar(
            cliente, Lista(Fila("111", codigoSede: "NORTE"), Fila("222"), Fila("333")));

        handler.Requests.Should().HaveCount(3);
        handler.Requests.Should().OnlyContain(r => r.Metodo == HttpMethod.Post && r.Ruta == "/api/colaboradores");
        var bodies = handler.Requests.Select(r => JsonNode.Parse(r.Cuerpo!)!).OrderBy(b => b["numeroIdentificacion"]!.GetValue<string>()).ToList();
        bodies.Select(b => b["numeroIdentificacion"]!.GetValue<string>()).Should().Equal("111", "222", "333");
        bodies[0]["tipoIdentificacion"]!.GetValue<string>().Should().Be("CC");
        bodies[0]["primerNombre"]!.GetValue<string>().Should().Be("Ana");
        bodies[0]["primerApellido"]!.GetValue<string>().Should().Be("Perez");
        bodies[0]["codigoColaborador"]!.GetValue<string>().Should().Be("C111");
        bodies[0]["fechaInicio"]!.GetValue<string>().Should().Be("2026-09-01");
        bodies[0]["codigoSede"]!.GetValue<string>().Should().Be("NORTE");

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["resultado"]!.GetValue<string>().Should().Be(RegistrarColaboradoresTool.Mensajes.ResultadoColaboradoresRegistrados);
        json["registrados"]!.GetValue<int>().Should().Be(3);
        json.ContainsKey("ya_registrados").Should().BeFalse();
        json.ContainsKey("rechazados").Should().BeFalse();
        json["nota"]!.GetValue<string>().Should().Be(RegistrarColaboradoresTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task RegistrarColaboradores_PonePorFilaEnYaRegistradosYRechazados_Cuando409Y400SinDetenerAlResto()
    {
        var (cliente, handler) = ClienteConStatusPorNumero(
            n => n switch
            {
                "222" => HttpStatusCode.Conflict,
                "333" => HttpStatusCode.BadRequest,
                _ => HttpStatusCode.Created
            },
            cuerpoRechazo: "codigo invalido");

        var resultado = await Ejecutar(
            cliente, Lista(Fila("111"), Fila("222", tipo: "cc"), Fila("333"), Fila("444")));

        handler.Requests.Should().HaveCount(4);
        var json = JsonNode.Parse(resultado)!;
        json["registrados"]!.GetValue<int>().Should().Be(2);
        json["ya_registrados"]!.AsArray().Select(n => n!.GetValue<string>()).Should().Equal("cc-222");
        var rechazados = json["rechazados"]!.AsArray();
        rechazados.Should().HaveCount(1);
        rechazados[0]!["fila"]!.GetValue<int>().Should().Be(3);
        rechazados[0]!["identificacion"]!.GetValue<string>().Should().Be("CC-333");
        rechazados[0]!["motivo"]!.GetValue<string>()
            .Should().Be(string.Format(RegistrarColaboradoresTool.Mensajes.RechazoDelDominio, "codigo invalido"));
    }

    [Fact]
    public async Task RegistrarColaboradores_RechazaLaFilaSinEnviarla_CuandoFaltaFechaInicio()
    {
        var (cliente, handler) = ClienteConStatusPorNumero(_ => HttpStatusCode.Created);

        var resultado = await Ejecutar(cliente, Lista(Fila("111"), Fila("222", fechaInicio: null), Fila("333")));

        handler.Requests.Select(r => JsonNode.Parse(r.Cuerpo!)!["numeroIdentificacion"]!.GetValue<string>())
            .Should().BeEquivalentTo("111", "333");
        var json = JsonNode.Parse(resultado)!;
        json["registrados"]!.GetValue<int>().Should().Be(2);
        var rechazado = json["rechazados"]!.AsArray().Single()!;
        rechazado["fila"]!.GetValue<int>().Should().Be(2);
        rechazado["identificacion"]!.GetValue<string>().Should().Be("CC-222");
        rechazado["motivo"]!.GetValue<string>()
            .Should().Be(string.Format(RegistrarColaboradoresTool.Mensajes.CampoObligatorio, "fecha_inicio"));
    }

    [Fact]
    public async Task RegistrarColaboradores_RechazaLaFilaSinEnviarla_CuandoUnObligatorioViajaVacio()
    {
        var (cliente, handler) = ClienteConStatusPorNumero(_ => HttpStatusCode.Created);

        var resultado = await Ejecutar(cliente, Lista(Fila("111", primerNombre: "  "), Fila("222")));

        handler.Requests.Should().ContainSingle();
        var rechazado = JsonNode.Parse(resultado)!["rechazados"]!.AsArray().Single()!;
        rechazado["fila"]!.GetValue<int>().Should().Be(1);
        rechazado["motivo"]!.GetValue<string>()
            .Should().Be(string.Format(RegistrarColaboradoresTool.Mensajes.CampoObligatorio, "primer_nombre"));
    }

    [Fact]
    public async Task RegistrarColaboradores_RechazaLaFilaSinEnviarla_CuandoFechaInicioTieneFormatoInvalido()
    {
        var (cliente, handler) = ClienteConStatusPorNumero(_ => HttpStatusCode.Created);

        var resultado = await Ejecutar(cliente, Lista(Fila("111", fechaInicio: "01/09/2026")));

        handler.Requests.Should().BeEmpty();
        var json = JsonNode.Parse(resultado)!;
        json["registrados"]!.GetValue<int>().Should().Be(0);
        json["rechazados"]!.AsArray().Single()!["motivo"]!.GetValue<string>()
            .Should().Be(string.Format(RegistrarColaboradoresTool.Mensajes.FechaInvalida, "01/09/2026"));
    }

    [Fact]
    public async Task RegistrarColaboradores_ResponderElMensajeSinLlamarAlBackend_CuandoColaboradoresEstaVacio()
    {
        var (cliente, handler) = ClienteFalso.ConRutas();

        var resultado = await Ejecutar(cliente, "  ");

        resultado.Should().Be(RegistrarColaboradoresTool.Mensajes.ColaboradoresObligatorio);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RegistrarColaboradores_ResponderElMensajeSinLlamarAlBackend_CuandoNoEsJson()
    {
        var (cliente, handler) = ClienteFalso.ConRutas();

        var resultado = await Ejecutar(cliente, "no es json");

        resultado.Should().Be(RegistrarColaboradoresTool.Mensajes.ColaboradoresJsonInvalido);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RegistrarColaboradores_ResponderElMensajeSinLlamarAlBackend_CuandoLaListaEstaVacia()
    {
        var (cliente, handler) = ClienteFalso.ConRutas();

        var resultado = await Ejecutar(cliente, "[]");

        resultado.Should().Be(RegistrarColaboradoresTool.Mensajes.ColaboradoresListaVacia);
        handler.Requests.Should().BeEmpty();
    }
}
