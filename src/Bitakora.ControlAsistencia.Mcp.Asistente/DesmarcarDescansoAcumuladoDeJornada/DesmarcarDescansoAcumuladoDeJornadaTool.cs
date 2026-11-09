using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DesmarcarDescansoAcumuladoDeJornada;

public partial class DesmarcarDescansoAcumuladoDeJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "desmarcar_descanso_acumulado_de_jornada";

    [Function("DesmarcarDescansoAcumuladoDeJornada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita la marca de descanso acumulado: la semana sin dia de descanso vuelve a medirse semana a semana.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(jornadaId, out var id))
            return string.Format(Mensajes.JornadaIdInvalido, jornadaId);

        var hecha = await api.DesmarcarDescansoAcumuladoDeJornada(id, ct);
        if (await hecha.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var obtenida = await api.ObtenerJornada(id, ct);
        if (await obtenida.LeerFalloAsync(ct) is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);

        return RespuestaJson.Serializar(await obtenida.LeerDescritaAsync(ct));
    }
}
