using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantillaSemanal;

public partial class QuitarJornadaDePlantillaSemanalTool(
    ProgramacionApi programacion, ResolutorPlantillaPorNombre resolutorPlantilla)
{
    internal const string NombreTool = "quitar_jornada_de_plantilla_semanal";

    [Function("QuitarJornadaDePlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita la Jornada de una plantilla semanal (nombre exacto): la plantilla deja de auditarse "
            + "contra limites. Si no tenia Jornada, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla",
            "Nombre exacto de la plantilla semanal del catalogo (mirala con buscar_plantillas_semanales).",
            isRequired: true)]
        string plantilla,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plantilla))
            return string.Format(Mensajes.CampoObligatorio, "plantilla");

        var resolucion = await resolutorPlantilla.ResolverAsync(plantilla, ct);
        if (resolucion.FalloDeLectura is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);
        if (resolucion.Ficha is null)
            return string.Format(
                Mensajes.PlantillaNoExiste, plantilla, string.Join(", ", resolucion.NombresDisponibles));

        var respuesta = await programacion.QuitarJornadaDePlantillaSemanal(resolucion.Ficha.Id, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } motivo)
            return string.Format(Mensajes.RechazoDelDominio, motivo);

        return RespuestaJson.Serializar(new JornadaQuitadaResumen(
            Mensajes.ResultadoJornadaQuitada, resolucion.Ficha.Nombre, Mensajes.NotaVisibilidadEventual));
    }
}

public sealed record JornadaQuitadaResumen(string Resultado, string Plantilla, string Nota);
