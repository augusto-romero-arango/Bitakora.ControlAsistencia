using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantillaSemanal;

public partial class QuitarJornadaDePlantillaSemanalTool(
    ProgramacionApi programacion, ResolutorPlantillaPorId resolutorPlantilla)
{
    internal const string NombreTool = "quitar_jornada_de_plantilla_semanal";

    [Function("QuitarJornadaDePlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita la Jornada de una plantilla semanal (por su id): la plantilla deja de auditarse "
            + "contra limites. Si no tenia Jornada, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla semanal, tal como lo da buscar_plantillas_semanales.", isRequired: true)]
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

        var respuesta = await programacion.QuitarJornadaDePlantillaSemanal(plantilla.Id, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } motivo)
            return string.Format(Mensajes.RechazoDelDominio, motivo);

        return RespuestaJson.Serializar(new JornadaQuitadaResumen(
            Mensajes.ResultadoJornadaQuitada, plantilla.Nombre, Mensajes.NotaVisibilidadEventual));
    }
}

public sealed record JornadaQuitadaResumen(string Resultado, string Plantilla, string Nota);
