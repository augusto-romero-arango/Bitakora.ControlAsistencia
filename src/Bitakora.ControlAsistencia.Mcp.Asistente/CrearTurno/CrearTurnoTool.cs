using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurno;

// Cliente HTTP puro del comando CrearTurno (POST /api/programacion/turnos, paso 1
// MEF-ADR-0043): el turnoId es un Guid v7 generado por esta tool (MEF-ADR-0037 seccion 1), nunca
// por el dominio. El rechazo del dominio (nombre duplicado) se devuelve como texto, nunca como
// excepcion (CA-ADR-0030).
public partial class CrearTurnoTool(ProgramacionApi programacion, ResolutorSedePorCodigo resolutorSedes)
{
    internal const string NombreTool = "crear_turno";

    [Function("CrearTurno")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea un turno nuevo del catalogo. Por defecto nace vacio (turno incompleto): existe "
            + "y se puede seguir disenando con agregar_franja, pero no se puede programar hasta "
            + "tener al menos una franja ordinaria. Si ya conoces el turno completo, pasa sus "
            + "franjas con sus descansos, extras y sede en la misma llamada (parametro franjas): nace "
            + "completo y programable, y si una franja es invalida no se crea nada. Horas sin "
            + "offsets: si el fin es menor que el inicio la franja cruza la medianoche, y si el "
            + "inicio es igual al fin dura 24 horas. Con es_descanso en true crea un turno de "
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
        [McpToolProperty(
            "franjas",
            "Franjas del turno completo, como JSON (opcional); cada franja, descanso y extra en formato HH:mm-HH:mm: "
            + "[{\"franja\":\"06:00-14:00\",\"descansos\":[\"10:00-10:15\"],\"extras\":[\"13:00-14:00\"],\"codigo_sede\":\"SUBA\"}]. "
            + "descansos, extras y codigo_sede son opcionales (codigo_sede, miralo con buscar_sedes).")]
        string? franjas,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Format(Mensajes.CampoObligatorio, "nombre");

        var descanso = esDescanso ?? false;
        var turnoId = Guid.CreateVersion7();

        IReadOnlyList<FranjaLeida> leidas = [];
        if (!string.IsNullOrWhiteSpace(franjas))
        {
            var lectura = FranjasDeTurno.Leer(franjas);
            if (lectura.Error is not null)
                return lectura.Error;
            leidas = lectura.Franjas;
        }

        var sedes = new Dictionary<string, SedeProgramada>();
        foreach (var codigo in leidas.Select(f => f.CodigoSede).OfType<string>().Distinct())
        {
            var resolucionSede = await resolutorSedes.ResolverAsync(codigo, ct);
            if (resolucionSede.FalloDeLectura is { } falloSede)
                return string.Format(Mensajes.RechazoDelDominio, falloSede);
            if (resolucionSede.MensajeDelMotivo(
                codigo, noExiste: Mensajes.SedeNoExiste, inactiva: Mensajes.SedeInactiva) is { } rechazo)
                return rechazo;

            sedes[codigo] = resolucionSede.Sede!;
        }

        // franjas "[]" equivale a omitirlas: el turno nace vacio y el eco no lo declara completo.
        var conSede = leidas.Select(f => (Franja: f, Sede: f.CodigoSede is null ? null : sedes[f.CodigoSede])).ToList();
        IReadOnlyList<OrdinariaACrear>? ordinarias =
            conSede.Count == 0 ? null : conSede.Select(x => x.Franja.ParaEnviar(x.Sede)).ToList();
        IReadOnlyList<string>? franjasEco =
            conSede.Count == 0 ? null : conSede.Select(x => x.Franja.Compactar(x.Sede?.Nombre)).ToList();

        var respuesta = await programacion.CrearTurno(turnoId, nombre, descanso, ct, ordinarias);

        if (!respuesta.IsSuccessStatusCode)
            return string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(ct));

        return RespuestaJson.Serializar(new TurnoCreadoResumen(
            Mensajes.ResultadoTurnoCreado,
            new TurnoCreadoEco(turnoId.ToString(), nombre, descanso, descanso || franjasEco is not null, franjasEco),
            Mensajes.NotaVisibilidadEventual));
    }
}

/// <summary>
/// Eco compacto de crear_turno hacia el asistente: el 201 del dominio no trae body, asi que el
/// turno creado se reconstruye con lo que entro a la tool mas el id generado por ella.
/// Un turno recien creado esta completo si es descanso o si nacio con franjas.
/// </summary>
public sealed record TurnoCreadoResumen(string Resultado, TurnoCreadoEco Turno, string Nota);

public sealed record TurnoCreadoEco(
    string Id, string Nombre, bool EsDescanso, bool Completo, IReadOnlyList<string>? Franjas = null);
