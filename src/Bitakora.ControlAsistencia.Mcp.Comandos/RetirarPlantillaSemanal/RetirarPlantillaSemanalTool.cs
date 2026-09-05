using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.RetirarPlantillaSemanal;

// Cliente HTTP puro de GET programacion/plantillas-semanales (resolver nombre -> id, via
// ResolutorPlantillaPorNombre) + DELETE programacion/plantillas-semanales/{id} (paso 3
// MEF-ADR-0043): referencia por nombre, mismo criterio que retirar_turno (MEF-ADR-0018). El
// 404/409 del DELETE se devuelve como texto, nunca como excepcion (CA-ADR-0030).
public partial class RetirarPlantillaSemanalTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "retirar_plantilla_semanal";

    private readonly ResolutorPlantillaPorNombre resolutor = new(programacion);

    [Function("RetirarPlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Retira una plantilla semanal del catalogo por su nombre exacto (mirala con "
            + "listar_plantillas_semanales): deja de poder usarse y su nombre queda libre. Los "
            + "turnos del catalogo no cambian.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla",
            "Nombre exacto de la plantilla semanal del catalogo (mirala con listar_plantillas_semanales).",
            isRequired: true)]
        string plantilla,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plantilla))
            return string.Format(Mensajes.CampoObligatorio, "plantilla");

        var resolucion = await resolutor.ResolverAsync(plantilla, ct);
        if (resolucion.FalloDeLectura is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);
        if (resolucion.Ficha is null)
            return string.Format(
                Mensajes.PlantillaNoExiste, plantilla, string.Join(", ", resolucion.NombresDisponibles));

        var ficha = resolucion.Ficha;
        var respuesta = await programacion.RetirarPlantillaSemanal(ficha.Id, ct);

        if (!respuesta.IsSuccessStatusCode)
            return string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(ct));

        return RespuestaJson.Serializar(new PlantillaRetiradaResumen(
            Mensajes.ResultadoPlantillaRetirada,
            new PlantillaRetiradaEco(ficha.Id, ficha.Nombre),
            Mensajes.NotaVisibilidadEventual));
    }
}

/// <summary>
/// Eco compacto de retirar_plantilla_semanal hacia el asistente: el 204 del dominio no trae body,
/// asi que la plantilla retirada se reconstruye con la ficha ya resuelta por nombre.
/// </summary>
public sealed record PlantillaRetiradaResumen(string Resultado, PlantillaRetiradaEco Plantilla, string Nota);

public sealed record PlantillaRetiradaEco(string Id, string Nombre);
