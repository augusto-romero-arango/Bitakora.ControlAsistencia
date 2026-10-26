using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AgregarSubFranja;

// Cliente HTTP puro que envuelve GET programacion/turnos/{id} (detalle del turno, via
// ResolutorTurnoPorNombre) + POST programacion/turnos/{id}:agregar-subfranja (paso 4
// MEF-ADR-0043). Descanso y extra comparten forma exacta: una tool con tipo, no cuatro
// (MEF-ADR-0047 decision 4). El 400/404/409 del POST se traduce a texto, nunca a excepcion
// (CA-ADR-0030).
public partial class AgregarSubFranjaTool(ProgramacionApi programacion, ResolutorTurnoPorId resolutor)
{
    internal const string NombreTool = "agregar_subfranja";

    [Function("AgregarSubFranja")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Agrega dentro de una franja ordinaria de un turno un descanso (pausa que se descuenta "
            + "del trabajo, ej. almuerzo) o un extra (horas suplementarias programadas). Indica el "
            + "turno por su id, la franja por su hora de inicio y el tipo: descanso o "
            + "extra. Horas en HH:mm; si la franja es nocturna, el sistema resuelve solo si el "
            + "descanso cae despues de la medianoche. Debe quedar contenido en la franja y no "
            + "solaparse con otros descansos o extras de la misma franja.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno_id", "Id del turno, tal como lo da buscar_turnos.", isRequired: true)]
        string turnoId,
        [McpToolProperty(
            "franja", "Hora de inicio de la franja ordinaria donde se agrega, formato HH:mm.", isRequired: true)]
        string franja,
        [McpToolProperty("tipo", "Tipo de sub-franja: 'descanso' o 'extra'.", isRequired: true)]
        string tipo,
        [McpToolProperty("inicio", "Hora de inicio del descanso o extra, formato HH:mm.", isRequired: true)]
        string inicio,
        [McpToolProperty("fin", "Hora de fin del descanso o extra, formato HH:mm.", isRequired: true)]
        string fin,
        CancellationToken ct)
    {
        if (!Guid.TryParse(turnoId, out var idTurno))
            return string.Format(Mensajes.TurnoIdInvalido, turnoId);
        if (string.IsNullOrWhiteSpace(franja))
            return string.Format(Mensajes.CampoObligatorio, "franja");
        if (string.IsNullOrWhiteSpace(tipo))
            return string.Format(Mensajes.CampoObligatorio, "tipo");
        if (string.IsNullOrWhiteSpace(inicio))
            return string.Format(Mensajes.CampoObligatorio, "inicio");
        if (string.IsNullOrWhiteSpace(fin))
            return string.Format(Mensajes.CampoObligatorio, "fin");

        if (!TipoSubFranja.TryNormalizar(tipo, out var tipoNormalizado))
            return string.Format(Mensajes.TipoDesconocido, tipo);

        if (!NotacionFranja.TryParseHora(franja, out var horaFranja))
            return string.Format(Mensajes.HoraInvalida, "franja", franja);
        if (!NotacionFranja.TryParseHora(inicio, out var horaInicio))
            return string.Format(Mensajes.HoraInvalida, "inicio", inicio);
        if (!NotacionFranja.TryParseHora(fin, out var horaFin))
            return string.Format(Mensajes.HoraInvalida, "fin", fin);

        var resolucion = await resolutor.ResolverAsync(idTurno, ct);
        if (resolucion.FalloDeLectura is { } falloTurnos)
            return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        if (resolucion.Ficha is not { } fichaTurno)
            return string.Format(Mensajes.TurnoNoExiste, turnoId);

        var respuestaAgregar = await programacion.AgregarSubFranja(
            fichaTurno.Id, new SubFranjaAAgregar(horaFranja, tipoNormalizado, horaInicio, horaFin), ct);
        if (await respuestaAgregar.LeerFalloAsync(ct) is { } falloAgregar)
            return string.Format(Mensajes.RechazoDelDominio, falloAgregar);

        var subFranja =
            $"{tipoNormalizado} {NotacionFranja.Rango(horaInicio, horaFin, diaOffsetInicio: 0, diaOffsetFin: 0)}";

        return RespuestaJson.Serializar(new SubFranjaAgregadaResumen(
            Mensajes.ResultadoSubFranjaAgregada,
            fichaTurno.Nombre,
            NotacionFranja.Hora(horaFranja),
            subFranja,
            Mensajes.NotaVisibilidadEventual));
    }
}

/// <summary>
/// Eco compacto de agregar_subfranja hacia el asistente: el 202 del dominio no trae body, asi que
/// la sub-franja agregada se reconstruye con lo que se envio a la tool, en notacion compacta.
/// </summary>
public sealed record SubFranjaAgregadaResumen(
    string Resultado, string Turno, string Franja, string SubFranja, string Nota);
