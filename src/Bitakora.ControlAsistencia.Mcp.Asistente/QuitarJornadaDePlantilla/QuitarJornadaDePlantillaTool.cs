using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantilla;

public partial class QuitarJornadaDePlantillaTool(
    ProgramacionApi programacion, ResolutorPlantillaTurnosPorId resolutorPlantilla)
{
    internal const string NombreTool = "quitar_jornada_de_plantilla";

    [Function("QuitarJornadaDePlantilla")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita la Jornada de una plantilla de turnos (por su id): la plantilla deja de auditarse "
            + "contra limites. Si no tenia Jornada, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(plantillaId, out var idDePlantilla))
            return string.Format(Mensajes.PlantillaIdInvalido, plantillaId);

        var resolucion = await resolutorPlantilla.ResolverAsync(idDePlantilla, ct);
        if (resolucion.FalloDeLectura is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);
        if (resolucion.Ficha is not { } plantilla)
            return string.Format(Mensajes.PlantillaNoExiste, plantillaId);

        var respuesta = await programacion.QuitarJornadaDePlantillaTurnos(plantilla.Id, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } motivo)
            return string.Format(Mensajes.RechazoDelDominio, motivo);

        return RespuestaJson.Serializar(new JornadaQuitadaDePlantillaResumen(
            Mensajes.ResultadoJornadaQuitada, plantilla.Nombre, Mensajes.NotaVisibilidadEventual));
    }
}

public sealed record JornadaQuitadaDePlantillaResumen(string Resultado, string Plantilla, string Nota);
