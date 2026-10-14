using System.Text.Json.Nodes;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DesactivarJornada;

public partial class DesactivarJornadaTool(
    ProgramacionApi programacion, ColaboradoresApi colaboradores, TimeProvider? reloj = null)
{
    internal const string NombreTool = "desactivar_jornada";
    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    [Function("DesactivarJornada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Desactiva una Jornada del catalogo: deja de ofrecerse para asignar, pero lo ya programado "
            + "conserva su copia. No se puede desactivar si la usa algun colaborador vigente, si es la "
            + "predeterminada o si la usa una plantilla semanal; en ese caso hay que reasignar primero "
            + "(asignar_jornada, asignar_jornada_por_grupo, asignar_jornada_predeterminada).")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Id de la Jornada (el que da listar_jornadas).", isRequired: true)]
        string jornadaId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(jornadaId, out var id))
            return string.Format(Mensajes.JornadaIdInvalido, jornadaId);

        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(), ZonaBogota).DateTime);
        var fichas = await colaboradores.ListarFichas(hoy, null, [], null, ct, null, id);
        if (await fichas.LeerFalloAsync(ct) is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);

        var elementos = JsonNode.Parse(await fichas.Content.ReadAsStringAsync(ct))?["elementos"]?.AsArray()
            ?? [];
        var vigentes = elementos.Count;
        if (vigentes > 0)
        {
            var conTerminacion = elementos.Count(e => e?["vigenteHasta"] is not null);
            var frase = vigentes == 1
                ? Mensajes.JornadaEnUsoUnColaborador
                : string.Format(Mensajes.JornadaEnUsoVariosColaboradores, vigentes);
            if (conTerminacion == 0)
                return frase;
            var terminaciones = conTerminacion == 1
                ? Mensajes.UnaTerminacionRegistrada
                : string.Format(Mensajes.VariasTerminacionesRegistradas, conTerminacion);
            return $"{frase}. {terminaciones}";
        }

        var respuesta = await programacion.DesactivarJornada(id, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        return Mensajes.ResultadoJornadaDesactivada;
    }
}
