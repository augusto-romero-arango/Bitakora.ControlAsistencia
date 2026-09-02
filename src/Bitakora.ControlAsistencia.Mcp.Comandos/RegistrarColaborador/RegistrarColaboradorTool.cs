using System.Globalization;
using System.Net;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarColaborador;

// Cliente HTTP puro del comando RegistrarColaborador (POST /api/colaboradores): esta tool no
// decide ninguna regla de negocio -- traduce el contrato HTTP vigente (MEF-ADR-0043) al remodelado
// token-eficiente de MEF-ADR-0047 decision 4, y devuelve el rechazo del dominio como texto en vez
// de excepcion (CA-ADR-0030). fecha_inicio es obligatoria y nunca se sustituye por "hoy" (doctrina
// bitemporal del BC, issue #574): a diferencia de fecha_referencia en lectura, es la fecha de un
// hecho persistido. La identificacion viaja en el eco tal como la envio el asistente, sin la
// normalizacion canonica que computa el dominio en su borde (MEF-ADR-0037).
public partial class RegistrarColaboradorTool(ColaboradoresApi api)
{
    internal const string NombreTool = "registrar_colaborador";

    [Function("RegistrarColaborador")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Pone a una persona bajo control de asistencia: crea el colaborador y abre su "
            + "vinculacion desde fecha_inicio. Obligatorios: tipo y numero de identificacion, "
            + "primer nombre, primer apellido, codigo_colaborador (solo letras, numeros y - . _ ~) "
            + "y fecha_inicio (yyyy-MM-dd, pidesela al usuario: nunca la asumas). Opcionales: "
            + "segundo nombre, segundo apellido y codigo_sede (obtenlo con listar_sedes). Si la "
            + "identificacion ya esta registrada responde el rechazo del dominio: reingresar a "
            + "alguien ya registrado es otra operacion. Aparece en listar_colaboradores en unos "
            + "segundos.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "tipo_identificacion",
            "Tipo de documento de identificacion: CC, CE, TI, PA o PT.",
            isRequired: true)]
        string tipoIdentificacion,
        [McpToolProperty(
            "numero_identificacion", "Numero de identificacion del colaborador.", isRequired: true)]
        string numeroIdentificacion,
        [McpToolProperty("primer_nombre", "Primer nombre del colaborador.", isRequired: true)]
        string primerNombre,
        [McpToolProperty("segundo_nombre", "Segundo nombre del colaborador (opcional).")]
        string? segundoNombre,
        [McpToolProperty("primer_apellido", "Primer apellido del colaborador.", isRequired: true)]
        string primerApellido,
        [McpToolProperty("segundo_apellido", "Segundo apellido del colaborador (opcional).")]
        string? segundoApellido,
        [McpToolProperty(
            "codigo_colaborador",
            "Codigo unico del colaborador (solo letras, numeros y - . _ ~).",
            isRequired: true)]
        string codigoColaborador,
        [McpToolProperty(
            "fecha_inicio",
            "Fecha desde la que el colaborador queda vinculado, formato yyyy-MM-dd. Pidesela "
            + "siempre al usuario: nunca la asumas como hoy.",
            isRequired: true)]
        string fechaInicio,
        [McpToolProperty(
            "codigo_sede",
            "Codigo de la sede donde queda vinculado (opcional, obtenlo con listar_sedes).")]
        string? codigoSede,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tipoIdentificacion))
            return string.Format(Mensajes.CampoObligatorio, "tipo_identificacion");

        if (string.IsNullOrWhiteSpace(numeroIdentificacion))
            return string.Format(Mensajes.CampoObligatorio, "numero_identificacion");

        if (string.IsNullOrWhiteSpace(primerNombre))
            return string.Format(Mensajes.CampoObligatorio, "primer_nombre");

        if (string.IsNullOrWhiteSpace(primerApellido))
            return string.Format(Mensajes.CampoObligatorio, "primer_apellido");

        if (string.IsNullOrWhiteSpace(codigoColaborador))
            return string.Format(Mensajes.CampoObligatorio, "codigo_colaborador");

        if (string.IsNullOrWhiteSpace(fechaInicio))
            return string.Format(Mensajes.CampoObligatorio, "fecha_inicio");

        if (!DateOnly.TryParseExact(
            fechaInicio, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return string.Format(Mensajes.FechaInvalida, fechaInicio);

        var respuesta = await api.Registrar(
            new RegistroColaboradorSolicitado(
                tipoIdentificacion, numeroIdentificacion, primerNombre, segundoNombre,
                primerApellido, segundoApellido, codigoColaborador, fecha, codigoSede),
            ct);

        if (await TraducirRechazo(respuesta, ct) is { } rechazo)
            return rechazo;

        respuesta.EnsureSuccessStatusCode();

        var nombre = string.Join(
            " ",
            new[] { primerNombre, segundoNombre, primerApellido, segundoApellido }
                .Where(p => !string.IsNullOrWhiteSpace(p)));

        return RespuestaJson.Serializar(new ColaboradorRegistradoResumen(
            Mensajes.ResultadoColaboradorRegistrado,
            $"{tipoIdentificacion}-{numeroIdentificacion}",
            nombre,
            codigoColaborador,
            fechaInicio,
            codigoSede,
            Mensajes.NotaVisibilidadEventual));
    }

    private static async Task<string?> TraducirRechazo(HttpResponseMessage respuesta, CancellationToken ct) =>
        respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict
            ? string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(ct))
            : null;
}

/// <summary>
/// Eco compacto de registrar_colaborador hacia el asistente: el 202 del dominio no trae body y la
/// ficha se materializa asincronicamente, asi que el hecho registrado se reconstruye con lo que
/// entro a la tool.
/// </summary>
public sealed record ColaboradorRegistradoResumen(
    string Resultado,
    string Identificacion,
    string Nombre,
    string CodigoColaborador,
    string FechaInicio,
    string? CodigoSede,
    string Nota);
