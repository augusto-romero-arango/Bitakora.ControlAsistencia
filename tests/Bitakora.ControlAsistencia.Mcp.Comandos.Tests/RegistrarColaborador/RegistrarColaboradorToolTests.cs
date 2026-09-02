using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarColaborador;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.RegistrarColaborador;

public class RegistrarColaboradorToolTests
{
    // Valores por defecto del camino feliz -- cada test sobreescribe unicamente el campo que le
    // interesa via argumentos nombrados (9 parametros, MEF-ADR-0047 decision 4: consolidar antes
    // que partir).
    private static Task<string> Ejecutar(
        HttpClient cliente,
        string tipoIdentificacion = "CC",
        string numeroIdentificacion = "1098765432",
        string primerNombre = "Ana",
        string? segundoNombre = "Maria",
        string primerApellido = "Perez",
        string? segundoApellido = "Gomez",
        string codigoColaborador = "AMP01",
        string fechaInicio = "2026-09-01",
        string? codigoSede = "NORTE",
        CancellationToken ct = default)
    {
        var tool = new RegistrarColaboradorTool(new ColaboradoresApi(cliente));
        return tool.Run(
            context: null!,
            tipoIdentificacion: tipoIdentificacion,
            numeroIdentificacion: numeroIdentificacion,
            primerNombre: primerNombre,
            segundoNombre: segundoNombre,
            primerApellido: primerApellido,
            segundoApellido: segundoApellido,
            codigoColaborador: codigoColaborador,
            fechaInicio: fechaInicio,
            codigoSede: codigoSede,
            ct: ct);
    }

    [Fact]
    public async Task RegistrarColaborador_EnviaElBodyCamelCaseYDevuelveElEcoCompacto_Cuando202()
    {
        var (cliente, handler) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(cliente, ct: TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Should().Be(HttpMethod.Post);
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/colaboradores");

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!;
        body["tipoIdentificacion"]!.GetValue<string>().Should().Be("CC");
        body["numeroIdentificacion"]!.GetValue<string>().Should().Be("1098765432");
        body["primerNombre"]!.GetValue<string>().Should().Be("Ana");
        body["segundoNombre"]!.GetValue<string>().Should().Be("Maria");
        body["primerApellido"]!.GetValue<string>().Should().Be("Perez");
        body["segundoApellido"]!.GetValue<string>().Should().Be("Gomez");
        body["codigoColaborador"]!.GetValue<string>().Should().Be("AMP01");
        body["fechaInicio"]!.GetValue<string>().Should().Be("2026-09-01");
        body["codigoSede"]!.GetValue<string>().Should().Be("NORTE");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be("Colaborador registrado");
        json["identificacion"]!.GetValue<string>().Should().Be("CC-1098765432");
        json["nombre"]!.GetValue<string>().Should().Be("Ana Maria Perez Gomez");
        json["codigoColaborador"]!.GetValue<string>().Should().Be("AMP01");
        json["fechaInicio"]!.GetValue<string>().Should().Be("2026-09-01");
        json["codigoSede"]!.GetValue<string>().Should().Be("NORTE");
        json["nota"]!.GetValue<string>()
            .Should().Be(RegistrarColaboradorTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task RegistrarColaborador_OmiteCodigoSedeEnElEcoYArmaElNombreSinSegundos_CuandoNoSeEnvian()
    {
        var (cliente, _) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(
            cliente,
            segundoNombre: null,
            segundoApellido: null,
            codigoSede: null,
            ct: TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json.ContainsKey("codigoSede").Should().BeFalse("codigo_sede no llego en la llamada");
        json["nombre"]!.GetValue<string>().Should().Be("Ana Perez");
    }

    // MEF-ADR-0037: la tool no replica la normalizacion canonica del dominio -- la identificacion
    // viaja al dominio y vuelve en el eco tal como el asistente la escribio.
    [Fact]
    public async Task RegistrarColaborador_PreservaLaIdentificacionSinNormalizar_CuandoLlegaEnMinusculas()
    {
        var (cliente, handler) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(
            cliente, tipoIdentificacion: "cc", ct: TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!;
        body["tipoIdentificacion"]!.GetValue<string>().Should().Be("cc");

        var json = JsonNode.Parse(resultado)!;
        json["identificacion"]!.GetValue<string>().Should().Be("cc-1098765432");
    }

    [Fact]
    public async Task RegistrarColaborador_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Fixtures.Leer("RegistrarColaborador", "validacion-400.json");
        var (cliente, _) = ClienteFalso.Con(fixture, HttpStatusCode.BadRequest);

        var resultado = await Ejecutar(cliente, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RegistrarColaboradorTool.Mensajes.RechazoDelDominio, fixture));
    }

    [Fact]
    public async Task RegistrarColaborador_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "Ya existe un colaborador registrado con esa identificacion";
        var (cliente, _) = ClienteFalso.Con(cuerpo, HttpStatusCode.Conflict);

        var resultado = await Ejecutar(cliente, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RegistrarColaboradorTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task RegistrarColaborador_RechazaSinLlamarAlDominio_CuandoTipoIdentificacionEstaEnBlanco()
    {
        var (cliente, handler) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(
            cliente, tipoIdentificacion: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(RegistrarColaboradorTool.Mensajes.CampoObligatorio, "tipo_identificacion"));
        handler.UltimaRequest.Should().BeNull("un tipo_identificacion en blanco no debe llegar al dominio");
    }

    [Fact]
    public async Task RegistrarColaborador_RechazaSinLlamarAlDominio_CuandoNumeroIdentificacionEstaEnBlanco()
    {
        var (cliente, handler) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(
            cliente, numeroIdentificacion: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(RegistrarColaboradorTool.Mensajes.CampoObligatorio, "numero_identificacion"));
        handler.UltimaRequest.Should().BeNull("un numero_identificacion en blanco no debe llegar al dominio");
    }

    [Fact]
    public async Task RegistrarColaborador_RechazaSinLlamarAlDominio_CuandoPrimerNombreEstaEnBlanco()
    {
        var (cliente, handler) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(
            cliente, primerNombre: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(RegistrarColaboradorTool.Mensajes.CampoObligatorio, "primer_nombre"));
        handler.UltimaRequest.Should().BeNull("un primer_nombre en blanco no debe llegar al dominio");
    }

    [Fact]
    public async Task RegistrarColaborador_RechazaSinLlamarAlDominio_CuandoPrimerApellidoEstaEnBlanco()
    {
        var (cliente, handler) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(
            cliente, primerApellido: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(RegistrarColaboradorTool.Mensajes.CampoObligatorio, "primer_apellido"));
        handler.UltimaRequest.Should().BeNull("un primer_apellido en blanco no debe llegar al dominio");
    }

    [Fact]
    public async Task RegistrarColaborador_RechazaSinLlamarAlDominio_CuandoCodigoColaboradorEstaEnBlanco()
    {
        var (cliente, handler) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(
            cliente, codigoColaborador: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(RegistrarColaboradorTool.Mensajes.CampoObligatorio, "codigo_colaborador"));
        handler.UltimaRequest.Should().BeNull("un codigo_colaborador en blanco no debe llegar al dominio");
    }

    [Fact]
    public async Task RegistrarColaborador_RechazaSinLlamarAlDominio_CuandoFechaInicioEstaEnBlanco()
    {
        var (cliente, handler) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(
            cliente, fechaInicio: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(RegistrarColaboradorTool.Mensajes.CampoObligatorio, "fecha_inicio"));
        handler.UltimaRequest.Should().BeNull("una fecha_inicio en blanco no debe llegar al dominio");
    }

    // CA-3: fecha_inicio ausente NUNCA se sustituye por "hoy" -- llegar con formato invalido (o en
    // blanco, arriba) corta en el worker igual que cualquier otro requerido.
    [Fact]
    public async Task RegistrarColaborador_RechazaSinLlamarAlDominio_CuandoFechaInicioTieneFormatoInvalido()
    {
        var (cliente, handler) = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);

        var resultado = await Ejecutar(
            cliente, fechaInicio: "2026-99-99", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(RegistrarColaboradorTool.Mensajes.FechaInvalida, "2026-99-99"));
        handler.UltimaRequest.Should().BeNull("una fecha_inicio con formato invalido no debe llegar al dominio");
    }
}
