using System.Net;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerPlantilla;

public partial class ObtenerPlantillaTool(ProgramacionApi api)
{
    internal const string NombreTool = "obtener_plantilla";

    [Function("ObtenerPlantilla")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Devuelve el cuadro de una plantilla de turnos por su id (ubicala con buscar_plantillas): "
            + "por cada dia del ciclo, del 1 al N, el turno con su nombre y horario, los dias sin turno "
            + "y los turnos retirados o incompletos; la Jornada asociada y las advertencias de la "
            + "plantilla contra sus limites. Para las franjas de un turno usa obtener_turno.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(plantillaId, out var idDePlantilla))
            return string.Format(Mensajes.PlantillaIdInvalido, plantillaId);

        var respuesta = await api.ObtenerPlantillaTurnos(idDePlantilla.ToString(), ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return string.Format(Mensajes.PlantillaNoExiste, plantillaId);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var detalle = (await respuesta.Content.ReadFromJsonAsync<CuadroDeTurnos>(ct))!;

        // Un dia ausente rinde "sin turno": el cuadro materializado omite los vacios.
        var textos = new Dictionary<int, string>();
        foreach (var dia in detalle.Dias)
            textos[dia.Dia] = TextoDelDia(dia.Turno);

        var dias = Enumerable.Range(1, detalle.DiasDelCiclo)
            .Select(d => new DiaDeLaPlantilla(d, textos.GetValueOrDefault(d, Mensajes.SinTurno)))
            .ToList();

        return RespuestaJson.Serializar(new PlantillaDetallada(
            detalle.Id, detalle.Nombre.Trim(), detalle.DiasDelCiclo, detalle.Completa, dias,
            detalle.Jornada is null ? null : new JornadaDescritaResumen(detalle.Jornada.Id, detalle.Jornada.Descripcion),
            (detalle.Advertencias ?? []).Select(a => a.Descripcion).ToList()));
    }

    // Retirado gana sobre incompleto: sin ficha en el catalogo, Completo llega false por construccion.
    private static string TextoDelDia(TurnoDelCuadroDeTurnos turno) => turno switch
    {
        { Retirado: true } => string.Format(Mensajes.TurnoRetirado, turno.Nombre ?? turno.Id),
        { Completo: false } => string.Format(Mensajes.TurnoIncompleto, turno.Nombre),
        _ => $"{turno.Nombre} {turno.Descripcion}"
    };
}

public sealed record PlantillaDetallada(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    bool Completa,
    IReadOnlyList<DiaDeLaPlantilla> Dias,
    JornadaDescritaResumen? Jornada,
    IReadOnlyList<string> Advertencias);

public sealed record DiaDeLaPlantilla(int Dia, string Turno);
