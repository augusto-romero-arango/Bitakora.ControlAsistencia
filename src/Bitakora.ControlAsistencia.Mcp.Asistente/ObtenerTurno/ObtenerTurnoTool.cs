using System.Net;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerTurno;

// Tool de solo lectura sobre GET programacion/turnos/{id}. Remodela cada franja como una linea
// compacta ("06:00-10:00, descanso 12:00-13:00, sede: Norte") en vez del arbol
// franjas/descansos/extras del endpoint. El 404 upstream se traduce a un mensaje en español: para
// el asistente "no existe" es una respuesta util, no un error.
public partial class ObtenerTurnoTool(ProgramacionApi api)
{
    internal const string NombreTool = "obtener_turno";

    [Function("ObtenerTurno")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Obtiene el detalle de un turno del catalogo: sus franjas con horario, descansos, "
            + "extras y sede prearmada si la tiene. Usalo para confirmar la composicion exacta "
            + "de un turno antes de programarlo. Para ubicar el turno por una mencion aproximada, usa buscar_turnos.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "id",
            "Id del turno en el catalogo (obtenlo con buscar_turnos).",
            isRequired: true)]
        string turnoId,
        CancellationToken ct)
    {
        var respuesta = await api.ObtenerTurno(turnoId, ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return string.Format(Mensajes.TurnoNoExiste, turnoId);

        respuesta.EnsureSuccessStatusCode();

        var ficha = (await respuesta.Content.ReadFromJsonAsync<FichaTurno>(ct))!;

        // Un turno de horario abierto controla horas y no horarios: su franja es detalle de diseno,
        // asi que solo se conserva su sede.
        var horarioAbierto = ficha.HorarioAbierto;
        var detalle = new TurnoDetallado(
            ficha.Id,
            ficha.Nombre.Trim(),
            ficha.EsDescanso,
            ficha.Completo,
            horarioAbierto is not null ? string.Format(Mensajes.HorarioAbierto, horarioAbierto.Duracion)
                : ficha.EsDescanso || !ficha.Completo ? ficha.HorarioResumido
                : NotacionFranja.Horario(ficha.Franjas),
            horarioAbierto is null ? [.. ficha.Franjas.Select(f => NotacionFranja.Compactar(f, Sede(f)))] : [],
            ficha.VentanaDeEntrada is { } v
                ? string.Format(Mensajes.EntraEntre, $"{v.Apertura:HH\\:mm}", $"{v.Cierre:HH\\:mm}", v.Amplitud)
                : null,
            horarioAbierto is null ? null : ficha.Franjas.Select(Sede).FirstOrDefault(s => s is not null));

        return RespuestaJson.Serializar(detalle);
    }

    private static string? Sede(FranjaFicha franja) => franja.NombreSede ?? franja.SedeId;
}

/// <summary>Contrato de respuesta de obtener_turno hacia el asistente (remodelado, issue #502).</summary>
public sealed record TurnoDetallado(
    string Id,
    string Nombre,
    bool EsDescanso,
    bool Completo,
    string Horario,
    IReadOnlyList<string> Franjas,
    string? VentanaDeEntrada,
    string? Sede = null);
