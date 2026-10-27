using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarTurnoADiaDePlantilla;

public partial class AsignarTurnoADiaDePlantillaTool(
    ProgramacionApi programacion, ResolutorPlantillaTurnosPorId resolutorPlantilla, ResolutorTurnoPorId resolutorTurno)
{
    internal const string NombreTool = "asignar_turno_a_dia_de_plantilla";

    [Function("AsignarTurnoADiaDePlantilla")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Pone o reemplaza el turno de un dia de una plantilla de turnos: id de la plantilla "
            + "(ubicala con buscar_plantillas), dia del ciclo (1..N) e id del turno (ubicalo con "
            + "buscar_turnos). Solo admite turnos completos; el descanso es un turno mas. Si el dia "
            + "ya tenia ese mismo turno, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        [McpToolProperty("dia", "Dia del ciclo de la plantilla (1..N).", isRequired: true)]
        int? dia,
        [McpToolProperty("turno_id", "Id del turno, tal como lo da buscar_turnos.", isRequired: true)]
        string turnoId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(plantillaId, out var idDePlantilla))
            return string.Format(Mensajes.PlantillaIdInvalido, plantillaId);

        if (!Guid.TryParse(turnoId, out var idDelTurno))
            return string.Format(Mensajes.TurnoIdInvalido, turnoId);

        if (dia is not { } diaValor)
            return string.Format(Mensajes.CampoObligatorio, "dia");

        if (diaValor < 1)
            return string.Format(Mensajes.DiaInvalido, diaValor);

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

        var respuesta = await programacion.AsignarTurnoADiaDePlantillaTurnos(plantilla.Id, diaValor, turno.Id, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } motivo)
            return string.Format(Mensajes.RechazoDelDominio, motivo);

        return RespuestaJson.Serializar(new TurnoAsignadoADiaDePlantillaResumen(
            Mensajes.ResultadoTurnoAsignado,
            plantilla.Nombre,
            diaValor,
            turno.Nombre,
            Mensajes.NotaVisibilidadEventual));
    }
}

public sealed record TurnoAsignadoADiaDePlantillaResumen(
    string Resultado, string Plantilla, int Dia, string Turno, string Nota);
