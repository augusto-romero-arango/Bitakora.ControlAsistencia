using System.Net;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarColaboradores;

// Resuelve PERSONAS concretas por nombre o identificacion; listar_colaboradores resuelve GRUPOS
// (sede, etiquetas). Son lentes distintas: esta tool no reemplaza a listar_colaboradores.
public partial class BuscarColaboradoresTool(ColaboradoresApi api)
{
    internal const string NombreTool = "buscar_colaboradores";
    internal const int MaximoColaboradores = 20;
    private const int TakeUpstream = MaximoColaboradores + 1;

    [Function("BuscarColaboradores")]
    public async Task<string> Run(
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
        CancellationToken ct)
    {
        var nombreNormalizado = Normalizar(nombre);
        var identificacionesNormalizadas = ParsearIdentificaciones(identificaciones);

        if (nombreNormalizado is null && identificacionesNormalizadas.Count == 0)
            return Mensajes.FaltaCriterio;

        var respuesta = await api.BuscarEnDirectorio(
            identificacionesNormalizadas, nombreNormalizado, TakeUpstream, ct);

        if (await TraducirRechazo(respuesta, ct) is { } rechazo)
            return rechazo;

        respuesta.EnsureSuccessStatusCode();

        var entradas = (await respuesta.Content.ReadFromJsonAsync<SobreDeLista<EntradaDirectorio>>(ct))?.Elementos
            ?? [];

        if (entradas.Count == 0)
            return string.Format(
                Mensajes.SinCoincidencias, DescribirCriterio(nombreNormalizado, identificacionesNormalizadas));

        var visibles = entradas.Take(MaximoColaboradores).Select(Remodelar).ToList();

        var notaTruncado = entradas.Count > MaximoColaboradores ? Mensajes.NotaTruncado : null;

        return RespuestaJson.Serializar(
            new CoincidenciasDeColaboradores(visibles.Count, notaTruncado, visibles));
    }

    private static async Task<string?> TraducirRechazo(HttpResponseMessage respuesta, CancellationToken ct) =>
        respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
            ? string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(ct))
            : null;

    private static string DescribirCriterio(string? nombre, IReadOnlyList<string> identificaciones) =>
        string.Join(", ", identificaciones.Prepend(nombre).OfType<string>());

    private static IReadOnlyList<string> ParsearIdentificaciones(string? identificaciones) =>
        string.IsNullOrWhiteSpace(identificaciones)
            ? []
            : [.. identificaciones
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static ColaboradorEncontrado Remodelar(EntradaDirectorio entrada) =>
        new(
            entrada.Identificacion,
            entrada.NombreCompleto.Trim(),
            Normalizar(entrada.CodigoColaborador),
            entrada.CodigoSede,
            entrada.VigenteDesde,
            entrada.VigenteHasta);

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>Sobre de desambiguacion: sin total ni cursor (CA-ADR-0039 decisiones 2 y 5).</summary>
public sealed record CoincidenciasDeColaboradores(
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
