using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarTurnoADia;

// Cliente HTTP puro que envuelve GET programacion/plantillas-semanales/{id} + GET
// programacion/turnos/{id} (detalles por identidad) + PUT
// programacion/plantillas-semanales/{id}/dias/{semana}/{dia} (paso 2 MEF-ADR-0043): correccion
// puntual de un solo dia sobre una plantilla ya creada (CA-ADR-0034 decision 4, dia = slot
// atomico). El 404/409 del PUT se traduce a texto, nunca a excepcion (CA-ADR-0030).
public partial class AsignarTurnoADiaTool(
    ProgramacionApi programacion, ResolutorPlantillaPorId resolutorPlantilla, ResolutorTurnoPorId resolutorTurno)
{
    internal const string NombreTool = "asignar_turno_a_dia";

    [Function("AsignarTurnoADia")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Pone o reemplaza el turno de un dia de una plantilla semanal: id de la "
            + "plantilla (ubicala con buscar_plantillas_semanales), semana (1..N, 1 por defecto), "
            + "dia (lunes..domingo o 1..7) y id del turno (ubicalo con "
            + "buscar_turnos). Solo admite turnos completos; el descanso es un turno mas. Si el "
            + "dia ya tenia ese mismo turno, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla semanal, tal como lo da buscar_plantillas_semanales.", isRequired: true)]
        string plantillaId,
        [McpToolProperty("turno_id", "Id del turno, tal como lo da buscar_turnos.", isRequired: true)]
        string turnoId,
        [McpToolProperty("dia", "Dia de la semana: lunes..domingo o 1..7.", isRequired: true)]
        string dia,
        [McpToolProperty("semana", "Semana de la plantilla (1..N). Por defecto 1.")]
        int? semana,
        CancellationToken ct)
    {
        if (!Guid.TryParse(plantillaId, out var idDePlantilla))
            return string.Format(Mensajes.PlantillaIdInvalido, plantillaId);

        if (!Guid.TryParse(turnoId, out var idDelTurno))
            return string.Format(Mensajes.TurnoIdInvalido, turnoId);

        if (string.IsNullOrWhiteSpace(dia))
            return string.Format(Mensajes.CampoObligatorio, "dia");

        if (!DiaSemanaMcp.TryParsear(dia, out var diaIso))
            return string.Format(Mensajes.DiaDesconocido, dia);

        var semanaValor = semana ?? 1;
        if (semanaValor < 1)
            return string.Format(Mensajes.SemanaInvalida, semanaValor);

        var resolucion = await resolutorPlantilla.ResolverAsync(idDePlantilla, ct);
        if (resolucion.FalloDeLectura is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);
        if (resolucion.Ficha is not { } plantilla)
            return string.Format(Mensajes.PlantillaNoExiste, plantillaId);

        var resolucionTurno = await resolutorTurno.ResolverAsync(idDelTurno, ct);
        if (resolucionTurno.FalloDeLectura is { } falloTurno)
            return string.Format(Mensajes.RechazoDelDominio, falloTurno);
        if (resolucionTurno.Ficha is not { } turno)
            return string.Format(Mensajes.TurnoNoExiste, turnoId);

        var respuesta = await programacion.AsignarTurnoADia(plantilla.Id, semanaValor, diaIso, turno.Id, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } motivo)
            return string.Format(Mensajes.RechazoDelDominio, motivo);

        return RespuestaJson.Serializar(new TurnoAsignadoResumen(
            Mensajes.ResultadoTurnoAsignado,
            plantilla.Nombre,
            semanaValor,
            DiaSemanaMcp.NombreDe(diaIso),
            turno.Nombre,
            Mensajes.NotaVisibilidadEventual));
    }
}

/// <summary>
/// Eco de asignar_turno_a_dia hacia el asistente: el 204 del PUT no trae body util, asi que el dia
/// se reconstruye con lo que entro a la tool y lo leido de los detalles.
/// </summary>
public sealed record TurnoAsignadoResumen(string Resultado, string Plantilla, int Semana, string Dia, string Turno, string Nota);
