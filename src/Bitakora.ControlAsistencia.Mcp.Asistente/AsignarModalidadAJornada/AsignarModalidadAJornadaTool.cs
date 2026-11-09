using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarModalidadAJornada;

public partial class AsignarModalidadAJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "asignar_modalidad_a_jornada";

    [Function("AsignarModalidadAJornada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Declara bajo que modalidad legal se pacto una Jornada: " + ModalidadesDeJornada.Enumeradas
            + ". Reemplaza la que tuviera. Con modalidad el sistema advierte contra la ley; no certifica cumplimiento.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        [McpToolProperty("modalidad", ModalidadesDeJornada.Enumeradas + ".", isRequired: true)]
        string modalidad,
        CancellationToken ct)
    {
        if (!Guid.TryParse(jornadaId, out var id))
            return string.Format(Mensajes.JornadaIdInvalido, jornadaId);
        if (!ModalidadesDeJornada.TryNormalizar(modalidad, out var reconocida))
            return string.Format(Mensajes.ModalidadInvalida, modalidad);

        var asignada = await api.AsignarModalidadAJornada(id, reconocida, ct);
        if (await asignada.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var obtenida = await api.ObtenerJornada(id, ct);
        if (await obtenida.LeerFalloAsync(ct) is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);

        return RespuestaJson.Serializar(await obtenida.LeerDescritaAsync(ct));
    }
}
