using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearJornada;

public partial class CrearJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "crear_jornada";
    private const int MaximoDiasDescanso = 6;

    [Function("CrearJornada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea una Jornada en el catalogo con sus cuatro limites. No puede repetir los limites de "
            + "otra Jornada existente.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
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
        if (!ValidadorDuracion.TryParsear(horasSemanales, out var semanales))
            return string.Format(Mensajes.DuracionInvalida, "horas_semanales", horasSemanales);
        if (!ValidadorDuracion.TryParsear(topeDiario, out var tope))
            return string.Format(Mensajes.DuracionInvalida, "tope_diario", topeDiario);
        if (!ValidadorDuracion.TryParsear(minimoDiario, out var minimo))
            return string.Format(Mensajes.DuracionInvalida, "minimo_diario", minimoDiario);
        if (diasDescansoPorSemana is < 0 or > MaximoDiasDescanso)
            return string.Format(Mensajes.DiasDescansoInvalido, diasDescansoPorSemana);

        var jornadaId = Guid.CreateVersion7();
        var creada = await api.CrearJornada(
            new JornadaACrear(jornadaId, semanales, tope, minimo, diasDescansoPorSemana), ct);
        if (await creada.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var obtenida = await api.ObtenerJornada(jornadaId.ToString(), ct);
        if (await obtenida.LeerFalloAsync(ct) is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);

        return RespuestaJson.Serializar(await obtenida.LeerDescritaAsync(ct));
    }
}
