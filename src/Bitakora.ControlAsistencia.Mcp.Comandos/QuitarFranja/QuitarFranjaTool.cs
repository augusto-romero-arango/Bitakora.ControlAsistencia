using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.QuitarFranja;

// Cliente HTTP puro que envuelve GET programacion/turnos (resolver nombre -> id + ficha vigente
// para el eco) + POST programacion/turnos/{id}:quitar-franja (paso 4 MEF-ADR-0043). El 404/409 del
// POST se traduce a texto, nunca a excepcion (CA-ADR-0030).
public partial class QuitarFranjaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "quitar_franja";

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);

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
            "turno", "Nombre exacto del turno del catalogo (miralo con listar_turnos).", isRequired: true)]
        string turno,
        [McpToolProperty("franja", "Hora de inicio de la franja a quitar, formato HH:mm.", isRequired: true)]
        string franja,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(turno))
            return string.Format(Mensajes.CampoObligatorio, "turno");
        if (string.IsNullOrWhiteSpace(franja))
            return string.Format(Mensajes.CampoObligatorio, "franja");

        if (!NotacionFranja.TryParseHora(franja, out var horaFranja))
            return string.Format(Mensajes.HoraInvalida, "franja", franja);

        var resolucion = await resolutor.ResolverAsync(turno, ct);
        if (resolucion.FalloDeLectura is { } falloTurnos)
            return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        if (resolucion.Ficha is null)
            return string.Format(
                Mensajes.TurnoNoExiste, turno, string.Join(", ", resolucion.NombresDisponibles));
        var fichaTurno = resolucion.Ficha;

        var respuestaQuitar = await programacion.QuitarFranja(fichaTurno.Id, horaFranja, ct);
        if (await respuestaQuitar.LeerFalloAsync(ct) is { } falloQuitar)
            return string.Format(Mensajes.RechazoDelDominio, falloQuitar);

        return RespuestaJson.Serializar(new FranjaQuitadaResumen(
            Mensajes.ResultadoFranjaQuitada,
            fichaTurno.Nombre,
            ComponerEco(fichaTurno, horaFranja),
            Mensajes.NotaVisibilidadEventual));
    }

    // El eco usa lo que la ficha vigente mostraba al momento de la llamada (visibilidad eventual):
    // si aun no mostraba la franja, solo la hora -- el POST ya se envio igual y el dominio decide
    // con 409 si en verdad no existia.
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

/// <summary>
/// Eco compacto de quitar_franja hacia el asistente: la franja quitada se compone con lo que
/// mostraba la ficha vigente al momento de la llamada, en notacion compacta.
/// </summary>
public sealed record FranjaQuitadaResumen(string Resultado, string Turno, string FranjaQuitada, string Nota);
