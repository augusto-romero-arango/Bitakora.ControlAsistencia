using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AgregarFranja;

// Cliente HTTP puro que envuelve GET programacion/turnos/{id} (detalle del turno, via
// ResolutorTurnoPorId) + GET sedes/fichas/{codigo} (solo si viene codigo_sede) + POST
// programacion/turnos/{id}:agregar-franja (paso 4 MEF-ADR-0043). El 400/404/409 del POST se
// traduce a texto, nunca a excepcion (CA-ADR-0030).
public partial class AgregarFranjaTool(
    ProgramacionApi programacion, ResolutorTurnoPorId resolutor, ResolutorSedePorCodigo resolutorSedes)
{
    internal const string NombreTool = "agregar_franja";

    [Function("AgregarFranja")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Agrega una franja ordinaria (segmento continuo de trabajo) a un turno del catalogo. "
            + "Horas en formato HH:mm; si fin es menor que inicio la "
            + "franja cruza la medianoche; inicio igual a fin significa una franja de 24 horas. "
            + "Opcional codigo_sede (obtenlo con buscar_sedes) para prearmar la sede de esa franja. "
            + "Una franja no puede solaparse con otra del mismo turno ni agregarse a un turno de "
            + "descanso. Con la primera franja el turno queda completo y se puede programar.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno_id", "Id del turno, tal como lo da buscar_turnos.", isRequired: true)]
        string turnoId,
        [McpToolProperty("inicio", "Hora de inicio de la franja, formato HH:mm.", isRequired: true)]
        string inicio,
        [McpToolProperty("fin", "Hora de fin de la franja, formato HH:mm.", isRequired: true)]
        string fin,
        [McpToolProperty(
            "codigo_sede",
            "Codigo de la sede a prearmar en esta franja (opcional, miralo con buscar_sedes).")]
        string? codigoSede,
        CancellationToken ct)
    {
        if (!Guid.TryParse(turnoId, out var idTurno))
            return string.Format(Mensajes.TurnoIdInvalido, turnoId);
        if (string.IsNullOrWhiteSpace(inicio))
            return string.Format(Mensajes.CampoObligatorio, "inicio");
        if (string.IsNullOrWhiteSpace(fin))
            return string.Format(Mensajes.CampoObligatorio, "fin");

        if (!NotacionFranja.TryParseHora(inicio, out var horaInicio))
            return string.Format(Mensajes.HoraInvalida, "inicio", inicio);
        if (!NotacionFranja.TryParseHora(fin, out var horaFin))
            return string.Format(Mensajes.HoraInvalida, "fin", fin);

        var resolucion = await resolutor.ResolverAsync(idTurno, ct);
        if (resolucion.FalloDeLectura is { } falloTurnos)
            return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        if (resolucion.Ficha is not { } fichaTurno)
            return string.Format(Mensajes.TurnoNoExiste, turnoId);

        SedeProgramada? sede = null;
        if (!string.IsNullOrWhiteSpace(codigoSede))
        {
            var resolucionSede = await resolutorSedes.ResolverAsync(codigoSede, ct);
            if (resolucionSede.FalloDeLectura is { } falloSede)
                return string.Format(Mensajes.RechazoDelDominio, falloSede);
            if (resolucionSede.MensajeDelMotivo(
                codigoSede, noExiste: Mensajes.SedeNoExiste, inactiva: Mensajes.SedeInactiva) is { } rechazo)
                return rechazo;

            sede = resolucionSede.Sede;
        }

        // inicio == fin es la unica lectura valida de una franja de 24h; el dominio infiere el +1
        // por su cuenta cuando fin < inicio, asi que el payload no envia ningun otro offset. El eco
        // replica esa misma inferencia.
        int? diaOffsetFin = horaInicio == horaFin ? 1 : null;

        var respuestaAgregar = await programacion.AgregarFranja(
            fichaTurno.Id, new FranjaAAgregar(horaInicio, horaFin, diaOffsetFin, sede), ct);
        if (await respuestaAgregar.LeerFalloAsync(ct) is { } falloAgregar)
            return string.Format(Mensajes.RechazoDelDominio, falloAgregar);

        var franja = NotacionFranja.Compactar(
            horaInicio, horaFin, horaFin <= horaInicio ? 1 : 0, [], [], sede?.Nombre);

        return RespuestaJson.Serializar(new FranjaAgregadaResumen(
            Mensajes.ResultadoFranjaAgregada, fichaTurno.Nombre, franja, Mensajes.NotaVisibilidadEventual));
    }
}

/// <summary>
/// Eco compacto de agregar_franja hacia el asistente: el 202 del dominio no trae body, asi que la
/// franja agregada se reconstruye con lo que se envio a la tool, en notacion compacta.
/// </summary>
public sealed record FranjaAgregadaResumen(string Resultado, string Turno, string Franja, string Nota);
