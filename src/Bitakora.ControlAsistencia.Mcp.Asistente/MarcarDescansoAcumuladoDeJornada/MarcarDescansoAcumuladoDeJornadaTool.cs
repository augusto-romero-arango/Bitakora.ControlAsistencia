using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.MarcarDescansoAcumuladoDeJornada;

public partial class MarcarDescansoAcumuladoDeJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "marcar_descanso_acumulado_de_jornada";

    [Function("MarcarDescansoAcumuladoDeJornada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Marca que en esta Jornada el descanso se acumula (art. 184): se debe un dia de descanso por cada semana de trabajo seguido, tomados en la semana siguiente al fin de la racha (ciclos 14x14, 20x10). Solo tiene efecto si la Jornada tiene modalidad.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(jornadaId, out var id))
            return string.Format(Mensajes.JornadaIdInvalido, jornadaId);

        var hecha = await api.MarcarDescansoAcumuladoDeJornada(id, ct);
        if (await hecha.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var obtenida = await api.ObtenerJornada(id, ct);
        if (await obtenida.LeerFalloAsync(ct) is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);

        return RespuestaJson.Serializar(await obtenida.LeerDescritaAsync(ct));
    }
}
