using System.Net;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarSede;

// Cliente HTTP puro del comando RegistrarSede (POST /api/sedes): esta tool no decide ninguna
// regla de negocio -- traduce el contrato HTTP vigente (MEF-ADR-0043) al remodelado
// token-eficiente de MEF-ADR-0047 decision 4, y devuelve el rechazo del dominio como texto en vez
// de excepcion (CA-ADR-0030).
public partial class RegistrarSedeTool(SedesApi api)
{
    internal const string NombreTool = "registrar_sede";

    [Function("RegistrarSede")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Registra una sede (lugar de trabajo) nueva de la empresa. Codigo y nombre son "
            + "obligatorios; el codigo identifica la sede en el resto del sistema (solo letras, "
            + "numeros y - . _ ~), ciudad y direccion son informativos. Si el codigo ya existe, "
            + "responde el rechazo del dominio: no modifica la sede existente. La sede nace "
            + "activa y aparece en listar_sedes en unos segundos.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "codigo",
            "Codigo unico de la sede (solo letras, numeros y - . _ ~); identifica la sede en el "
            + "resto del sistema.",
            isRequired: true)]
        string codigo,
        [McpToolProperty("nombre", "Nombre de la sede.", isRequired: true)]
        string nombre,
        [McpToolProperty("ciudad", "Ciudad donde esta ubicada la sede (informativo).")]
        string? ciudad,
        [McpToolProperty("direccion", "Direccion de la sede (informativo).")]
        string? direccion,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return string.Format(Mensajes.CampoObligatorio, "codigo");

        if (string.IsNullOrWhiteSpace(nombre))
            return string.Format(Mensajes.CampoObligatorio, "nombre");

        var respuesta = await api.Registrar(codigo, nombre, ciudad, direccion, ct);

        if (await TraducirRechazo(respuesta, ct) is { } rechazo)
            return rechazo;

        respuesta.EnsureSuccessStatusCode();

        return RespuestaJson.Serializar(new SedeRegistradaResumen(
            Mensajes.ResultadoSedeRegistrada, codigo, nombre, ciudad, direccion,
            Mensajes.NotaVisibilidadEventual));
    }

    private static async Task<string?> TraducirRechazo(HttpResponseMessage respuesta, CancellationToken ct) =>
        respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict
            ? string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(ct))
            : null;
}

/// <summary>
/// Eco compacto de registrar_sede hacia el asistente: el 202 del dominio no trae body y la
/// ficha se materializa asincronicamente, asi que el hecho registrado se reconstruye con lo
/// que entro a la tool.
/// </summary>
public sealed record SedeRegistradaResumen(
    string Resultado,
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion,
    string Nota);
