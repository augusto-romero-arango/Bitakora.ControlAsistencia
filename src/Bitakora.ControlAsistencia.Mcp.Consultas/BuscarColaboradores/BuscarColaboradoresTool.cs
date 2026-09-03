using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.BuscarColaboradores;

// Tool de solo lectura sobre QUERY colaboradores/directorio (issue #588): resuelve PERSONAS
// concretas por nombre o identificacion, distinto de listar_colaboradores que resuelve GRUPOS
// (sede, etiquetas). Grupos e individuos son lentes distintas (decision del experto,
// 2026-09-03): esta tool no reemplaza a listar_colaboradores.
public partial class BuscarColaboradoresTool(ColaboradoresApi api)
{
    internal const string NombreTool = "buscar_colaboradores";
    internal const int MaximoColaboradores = 20;
    internal const int TakeUpstream = 200;

    [Function("BuscarColaboradores")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Busca colaboradores concretos por nombre o por identificacion para saber a quien se "
            + "refiere el usuario. Por nombre: una o varias palabras completas, sin importar "
            + "acentos, mayusculas ni orden ('juan bermudez' encuentra a Juan Pablo Bermudez; "
            + "'juan' a todos los Juanes; no busca por fragmentos como 'berm'). Por "
            + "identificacion: completa ('CC-79879078') o solo el numero ('79879078'); varias "
            + "separadas por coma. Devuelve identificacion completa, nombre, codigo de "
            + "colaborador, sede de trabajo y vigencia de cada coincidencia, incluidos los "
            + "retirados (mira vigenteHasta). Si hay mas de una coincidencia, pregunta al usuario "
            + "cual es antes de actuar. Para grupos (por sede o etiquetas) usa "
            + "listar_colaboradores; para programar, pasa las identificaciones completas al "
            + "servidor de Comandos (solicitar_programacion_turno).")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "nombre",
            "Nombre o parte del nombre en palabras completas (ej. 'juan bermudez'). Ignora "
            + "acentos y mayusculas; el orden no importa.")]
        string? nombre,
        [McpToolProperty(
            "identificaciones",
            "Una o varias identificaciones separadas por coma, completas ('CC-79879078') o solo "
            + "el numero ('79879078, 10047766882').")]
        string? identificaciones,
        CancellationToken ct) =>
        throw new NotImplementedException();
}

/// <summary>Contrato de respuesta de buscar_colaboradores hacia el asistente (issue #588).</summary>
public sealed record CoincidenciasDeColaboradores(
    int Total,
    int Mostrando,
    string? Nota,
    IReadOnlyList<ColaboradorEncontrado> Colaboradores);

/// <summary>
/// Coincidencia remodelada token-eficiente: mismos nombres de campo que
/// <see cref="ListarColaboradores.ColaboradorFicha"/> para que el agente razone entre tools del
/// mismo servidor sin remapear -- tipoDocumento/numeroDocumento no viajan (redundantes con
/// Identificacion).
/// </summary>
public sealed record ColaboradorEncontrado(
    string Identificacion,
    string Nombre,
    string? CodigoColaborador,
    string? CodigoSede,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta);
