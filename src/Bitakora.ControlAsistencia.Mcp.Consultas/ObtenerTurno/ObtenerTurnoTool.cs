using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerTurno;

// Tool de solo lectura sobre GET programacion/turnos/{id}. Remodela cada franja como una linea
// compacta ("06:00-10:00, descanso 12:00-13:00, sede: Norte") en vez del arbol
// franjas/descansos/extras del endpoint. El 404 upstream se traduce a un mensaje en español: para
// el asistente "no existe" es una respuesta util, no un error.
public class ObtenerTurnoTool(ProgramacionApi api)
{
    internal const string NombreTool = "obtener_turno";

    [Function("ObtenerTurno")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Obtiene el detalle de un turno del catalogo: sus franjas con horario, descansos, "
            + "extras y sede prearmada si la tiene. Usalo para confirmar la composicion exacta "
            + "de un turno antes de programarlo.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "id",
            "Id del turno en el catalogo (obtenlo con listar_turnos).",
            isRequired: true)]
        string id,
        CancellationToken ct)
    {
        var respuesta = await api.ObtenerTurno(id, ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return $"No existe un turno con id '{id}' en el catalogo.";

        respuesta.EnsureSuccessStatusCode();

        var ficha = (await respuesta.Content.ReadFromJsonAsync<FichaTurno>(ct))!;

        var detalle = new TurnoDetallado(
            ficha.Id,
            ficha.Nombre.Trim(),
            ficha.EsDescanso,
            ficha.HorarioResumido,
            [.. ficha.Franjas.Select(Compactar)]);

        return RespuestaJson.Serializar(detalle);
    }

    private static string Compactar(FranjaFicha franja)
    {
        var texto = new StringBuilder(Rango(franja.HoraInicio, franja.HoraFin, franja.DiaOffsetFin));

        foreach (var descanso in franja.Descansos)
            texto.Append($", descanso {Rango(descanso.HoraInicio, descanso.HoraFin, descanso.DiaOffsetFin)}");

        foreach (var extra in franja.Extras)
            texto.Append($", extra {Rango(extra.HoraInicio, extra.HoraFin, extra.DiaOffsetFin)}");

        var sede = franja.NombreSede ?? franja.SedeId;
        if (sede is not null)
            texto.Append($", sede: {sede}");

        return texto.ToString();
    }

    private static string Rango(TimeOnly inicio, TimeOnly fin, int diaOffsetFin) =>
        $"{inicio:HH\\:mm}-{fin:HH\\:mm}{(diaOffsetFin > 0 ? $"(+{diaOffsetFin})" : "")}";
}

/// <summary>Contrato de respuesta de obtener_turno hacia el asistente (remodelado, issue #502).</summary>
public sealed record TurnoDetallado(
    string Id,
    string Nombre,
    bool EsDescanso,
    string Horario,
    IReadOnlyList<string> Franjas);
