using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarFranja;

public partial class QuitarFranjaTool(ProgramacionApi programacion, ResolutorTurnoPorId resolutor)
{
    internal const string NombreTool = "quitar_franja";

    [Function("QuitarFranja")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita de un turno la franja ordinaria que empieza a la hora indicada (HH:mm), junto "
            + "con sus descansos, extras y sede prearmada. Para corregir el horario de una franja, "
            + "quitala y agregala de nuevo. Si era la unica franja, el turno queda incompleto y "
            + "deja de poder programarse.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno_id", "Id del turno, tal como lo da buscar_turnos.", isRequired: true)]
        string turnoId,
        [McpToolProperty("franja", "Hora de inicio de la franja a quitar, formato HH:mm.", isRequired: true)]
        string franja,
        CancellationToken ct)
    {
        if (!Guid.TryParse(turnoId, out var idTurno))
            return string.Format(Mensajes.TurnoIdInvalido, turnoId);
        if (string.IsNullOrWhiteSpace(franja))
            return string.Format(Mensajes.CampoObligatorio, "franja");

        if (!NotacionFranja.TryParseHora(franja, out var horaFranja))
            return string.Format(Mensajes.HoraInvalida, "franja", franja);

        var resolucion = await resolutor.ResolverAsync(idTurno, ct);
        if (resolucion.FalloDeLectura is { } falloTurnos)
            return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        if (resolucion.Ficha is not { } fichaTurno)
            return string.Format(Mensajes.TurnoNoExiste, turnoId);

        var respuestaQuitar = await programacion.QuitarFranja(fichaTurno.Id, horaFranja, ct);
        if (await respuestaQuitar.LeerFalloAsync(ct) is { } falloQuitar)
            return string.Format(Mensajes.RechazoDelDominio, falloQuitar);

        return RespuestaJson.Serializar(new FranjaQuitadaResumen(
            Mensajes.ResultadoFranjaQuitada,
            fichaTurno.Nombre,
            ComponerEco(fichaTurno, horaFranja),
            Mensajes.NotaVisibilidadEventual));
    }

    // El eco usa la ficha vigente al momento de la llamada (visibilidad eventual): si aun no mostraba la
    // franja va solo la hora; el POST se envia igual y el dominio decide con 409 si no existia.
    private static string ComponerEco(FichaTurno ficha, TimeOnly hora) =>
        ficha.Franjas.FirstOrDefault(f => f.HoraInicio == hora) is { } franja
            ? NotacionFranja.Compactar(
                franja.HoraInicio,
                franja.HoraFin,
                franja.DiaOffsetFin,
                franja.Descansos,
                franja.Extras,
                franja.NombreSede ?? franja.SedeId)
            : NotacionFranja.Hora(hora);
}

public sealed record FranjaQuitadaResumen(string Resultado, string Turno, string FranjaQuitada, string Nota);
