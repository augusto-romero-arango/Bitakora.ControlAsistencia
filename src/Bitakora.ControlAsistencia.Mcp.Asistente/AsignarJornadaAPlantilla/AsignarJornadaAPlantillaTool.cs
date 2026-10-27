using System.Net;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantilla;

public partial class AsignarJornadaAPlantillaTool(
    ProgramacionApi programacion, ResolutorPlantillaTurnosPorId resolutorPlantilla)
{
    internal const string NombreTool = "asignar_jornada_a_plantilla";

    [Function("AsignarJornadaAPlantilla")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Asocia una Jornada a una plantilla de turnos (por su id) o la reemplaza si ya tenia otra. "
            + "La plantilla se audita contra los limites de esa Jornada; las advertencias se ven con "
            + "obtener_plantilla en unos segundos. El jornada_id sale de listar_jornadas.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(plantillaId, out var idDePlantilla))
            return string.Format(Mensajes.PlantillaIdInvalido, plantillaId);

        if (!Guid.TryParse(jornadaId, out var jornadaGuid))
            return string.Format(Mensajes.JornadaIdInvalido, jornadaId);

        var resolucion = await resolutorPlantilla.ResolverAsync(idDePlantilla, ct);
        if (resolucion.FalloDeLectura is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);
        if (resolucion.Ficha is not { } plantilla)
            return string.Format(Mensajes.PlantillaNoExiste, plantillaId);

        var respuestaJornada = await programacion.ObtenerJornada(jornadaGuid, ct);
        if (respuestaJornada.StatusCode == HttpStatusCode.NotFound)
            return Mensajes.JornadaNoExiste;
        if (await respuestaJornada.LeerFalloAsync(ct) is { } falloJornada)
            return string.Format(Mensajes.RechazoDelDominio, falloJornada);
        var jornada = await respuestaJornada.LeerDescritaAsync(ct);

        var respuesta = await programacion.AsignarJornadaAPlantillaTurnos(plantilla.Id, jornadaGuid, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } motivo)
            return string.Format(Mensajes.RechazoDelDominio, motivo);

        return RespuestaJson.Serializar(new JornadaAsignadaAPlantillaResumen(
            Mensajes.ResultadoJornadaAsignada,
            plantilla.Nombre,
            jornada.Resumir(),
            Mensajes.NotaAdvertenciasEventuales));
    }
}

public sealed record JornadaAsignadaAPlantillaResumen(
    string Resultado, string Plantilla, JornadaDescritaResumen Jornada, string Nota);
