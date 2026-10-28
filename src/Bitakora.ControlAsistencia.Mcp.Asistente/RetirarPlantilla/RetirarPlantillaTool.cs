using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarPlantilla;

public partial class RetirarPlantillaTool(ProgramacionApi programacion, ResolutorPlantillaTurnosPorId resolutor)
{
    internal const string NombreTool = "retirar_plantilla";

    [Function("RetirarPlantilla")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Retira una plantilla de turnos del catalogo por su id (ubicala con buscar_plantillas): "
            + "deja de poder usarse y su nombre queda libre. Los turnos del catalogo y lo ya "
            + "programado con ella no cambian.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(plantillaId, out var idDePlantilla))
            return string.Format(Mensajes.PlantillaIdInvalido, plantillaId);

        var resolucion = await resolutor.ResolverAsync(idDePlantilla, ct);
        if (resolucion.FalloDeLectura is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);
        if (resolucion.Ficha is not { } plantilla)
            return string.Format(Mensajes.PlantillaNoExiste, plantillaId);

        var respuesta = await programacion.RetirarPlantillaTurnos(plantilla.Id, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } motivo)
            return string.Format(Mensajes.RechazoDelDominio, motivo);

        return RespuestaJson.Serializar(new PlantillaTurnosRetiradaResumen(
            Mensajes.ResultadoPlantillaRetirada,
            new PlantillaTurnosRetiradaEco(plantilla.Id, plantilla.Nombre),
            Mensajes.NotaVisibilidadEventual));
    }
}

public sealed record PlantillaTurnosRetiradaResumen(string Resultado, PlantillaTurnosRetiradaEco Plantilla, string Nota);

public sealed record PlantillaTurnosRetiradaEco(string Id, string Nombre);
