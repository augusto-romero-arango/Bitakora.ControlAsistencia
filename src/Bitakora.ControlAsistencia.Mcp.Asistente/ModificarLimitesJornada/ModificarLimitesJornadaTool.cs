using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ModificarLimitesJornada;

public partial class ModificarLimitesJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "modificar_limites_jornada";
    private const int MaximoDiasDescanso = 6;

    [Function("ModificarLimitesJornada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Reemplaza los cuatro limites de una Jornada. No cambia lo ya programado: cada dia "
            + "programado conserva la copia que se estampo.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        [McpToolProperty("horas_semanales", "Horas semanales en formato H:mm, por ejemplo 42:00.", isRequired: true)]
        string horasSemanales,
        [McpToolProperty("tope_diario", "Tope diario en formato H:mm, por ejemplo 8:00.", isRequired: true)]
        string topeDiario,
        [McpToolProperty("minimo_diario", "Minimo diario en formato H:mm, por ejemplo 0:00.", isRequired: true)]
        string minimoDiario,
        [McpToolProperty(
            "dias_descanso_por_semana",
            "Dias de descanso por semana, entero de 0 a 6 (0 = no se controla).",
            isRequired: true)]
        int diasDescansoPorSemana,
        CancellationToken ct)
    {
        if (!Guid.TryParse(jornadaId, out var id))
            return string.Format(Mensajes.JornadaIdInvalido, jornadaId);
        if (!ValidadorDuracion.TryParsear(horasSemanales, out var semanales))
            return string.Format(Mensajes.DuracionInvalida, "horas_semanales", horasSemanales);
        if (!ValidadorDuracion.TryParsear(topeDiario, out var tope))
            return string.Format(Mensajes.DuracionInvalida, "tope_diario", topeDiario);
        if (!ValidadorDuracion.TryParsear(minimoDiario, out var minimo))
            return string.Format(Mensajes.DuracionInvalida, "minimo_diario", minimoDiario);
        if (diasDescansoPorSemana is < 0 or > MaximoDiasDescanso)
            return string.Format(Mensajes.DiasDescansoInvalido, diasDescansoPorSemana);

        var modificada = await api.ModificarLimitesJornada(
            id, new LimitesJornadaAEnviar(semanales, tope, minimo, diasDescansoPorSemana), ct);
        if (await modificada.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var obtenida = await api.ObtenerJornada(id, ct);
        if (await obtenida.LeerFalloAsync(ct) is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);

        return RespuestaJson.Serializar(await obtenida.LeerDescritaAsync(ct));
    }
}
