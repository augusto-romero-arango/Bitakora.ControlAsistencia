using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.CrearTurno;

// Cliente HTTP puro del comando CrearTurno (POST /api/programacion/turnos, paso 1
// MEF-ADR-0043): el turnoId es un Guid v7 generado por esta tool (MEF-ADR-0037 seccion 1), nunca
// por el dominio. El rechazo del dominio (nombre duplicado) se devuelve como texto, nunca como
// excepcion (CA-ADR-0030).
public partial class CrearTurnoTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "crear_turno";

    [Function("CrearTurno")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea un turno nuevo del catalogo. Por defecto nace vacio (turno incompleto): existe "
            + "y se puede seguir disenando con agregar_franja, pero no se puede programar hasta "
            + "tener al menos una franja ordinaria. Con es_descanso en true crea un turno de "
            + "descanso (dia libre programable, no admite franjas). El nombre es unico en el "
            + "catalogo, sin distinguir mayusculas ni espacios repetidos; si ya existe, responde "
            + "el rechazo del dominio. Aparece en listar_turnos en unos segundos.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico del turno en el catalogo.", isRequired: true)]
        string nombre,
        // bool? y no bool: cuando el cliente omite un parametro opcional, el converter de la
        // extension (ToolInvocationArgumentTypeConverter) no lo resuelve y el worker deja null en
        // el argumento; el ejecutor generado hace (bool)argumento, que sobre null revienta en
        // runtime. Con bool? el null viaja igual que en los string? opcionales del resto de las
        // tools. El inputSchema publicado no cambia: bool y bool? mapean ambos a "boolean".
        [McpToolProperty(
            "es_descanso",
            "En true crea un turno de descanso (dia libre programable, sin franjas). Por defecto "
            + "false: turno incompleto listo para disenar con agregar_franja.")]
        bool? esDescanso,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Format(Mensajes.CampoObligatorio, "nombre");

        var descanso = esDescanso ?? false;
        var turnoId = Guid.CreateVersion7();
        var respuesta = await programacion.CrearTurno(turnoId, nombre, descanso, ct);

        if (!respuesta.IsSuccessStatusCode)
            return string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(ct));

        return RespuestaJson.Serializar(new TurnoCreadoResumen(
            Mensajes.ResultadoTurnoCreado,
            new TurnoCreadoEco(turnoId.ToString(), nombre, descanso, descanso),
            Mensajes.NotaVisibilidadEventual));
    }
}

/// <summary>
/// Eco compacto de crear_turno hacia el asistente: el 202 del dominio no trae body, asi que el
/// turno creado se reconstruye con lo que entro a la tool mas el id generado por ella.
/// completo = esDescanso (un turno recien creado solo esta completo si es descanso).
/// </summary>
public sealed record TurnoCreadoResumen(string Resultado, TurnoCreadoEco Turno, string Nota);

public sealed record TurnoCreadoEco(string Id, string Nombre, bool EsDescanso, bool Completo);
