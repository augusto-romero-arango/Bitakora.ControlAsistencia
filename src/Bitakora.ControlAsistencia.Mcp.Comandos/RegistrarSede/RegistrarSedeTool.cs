using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarSede;

// Issue #573: primera tool real del servidor de Comandos, reemplaza la tool de ejemplo del
// scaffold. Consume RegistrarSede (POST /api/sedes, #456). El aggregate no cambia (MEF-ADR-0004);
// esta tool solo traduce el contrato HTTP vigente (MEF-ADR-0043) al remodelado token-eficiente
// (MEF-ADR-0047 decision 4).
public partial class RegistrarSedeTool(SedesApi api)
{
    internal const string NombreTool = "registrar_sede";

    [Function("RegistrarSede")]
    public Task<string> Run(
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
        CancellationToken ct) =>
        throw new NotImplementedException();
}

/// <summary>Eco compacto de registrar_sede hacia el asistente (remodelado, issue #573).</summary>
public sealed record SedeRegistradaResumen(
    string Resultado,
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion,
    string Nota);
