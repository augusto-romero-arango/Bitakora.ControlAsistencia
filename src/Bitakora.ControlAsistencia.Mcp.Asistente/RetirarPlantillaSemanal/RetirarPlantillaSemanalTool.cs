using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarPlantillaSemanal;

// Cliente HTTP puro de GET programacion/plantillas-semanales (resolver nombre -> id, via
// ResolutorPlantillaPorNombre) + DELETE programacion/plantillas-semanales/{id} (paso 3
// MEF-ADR-0043): referencia por nombre, mismo criterio que retirar_turno (MEF-ADR-0018). El
// 404/409 del DELETE se devuelve como texto, nunca como excepcion (CA-ADR-0030).
public partial class RetirarPlantillaSemanalTool(ProgramacionApi programacion, ResolutorPlantillaPorId resolutor)
{
    internal const string NombreTool = "retirar_plantilla_semanal";

    [Function("RetirarPlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Retira una plantilla semanal del catalogo por su id (ubicala con "
            + "buscar_plantillas_semanales): deja de poder usarse y su nombre queda libre. Los "
            + "turnos del catalogo no cambian.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla semanal, tal como lo da buscar_plantillas_semanales.", isRequired: true)]
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

        var respuesta = await programacion.RetirarPlantillaSemanal(plantilla.Id, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } falloDelete)
            return string.Format(Mensajes.RechazoDelDominio, falloDelete);

        return RespuestaJson.Serializar(new PlantillaRetiradaResumen(
            Mensajes.ResultadoPlantillaRetirada,
            new PlantillaRetiradaEco(plantilla.Id, plantilla.Nombre),
            Mensajes.NotaVisibilidadEventual));
    }
}

/// <summary>
/// Eco compacto de retirar_plantilla_semanal hacia el asistente: el 204 del dominio no trae body,
/// asi que la plantilla retirada se reconstruye con la ficha ya resuelta por nombre.
/// </summary>
public sealed record PlantillaRetiradaResumen(string Resultado, PlantillaRetiradaEco Plantilla, string Nota);

public sealed record PlantillaRetiradaEco(string Id, string Nombre);
