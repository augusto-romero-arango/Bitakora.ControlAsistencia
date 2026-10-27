using System.Text.Json;
using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaSemanal;

// Interfaz semanal sobre la plantilla de turnos generica: un ciclo de 7 dias, dia 1 = lunes ... dia 7 =
// domingo. Valida la forma de la entrada y delega la escritura en CreadorDePlantillaTurnos.
public partial class CrearPlantillaSemanalTool(
    ProgramacionApi programacion, ResolutorTurnoPorId resolutorPorId, ResolutorTurnoPorNombre resolutor)
{
    internal const string NombreTool = "crear_plantilla_semanal";
    internal const int SemanaUnica = 1;
    internal const int DiasPorSemana = 7;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("CrearPlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea una plantilla semanal de turnos: un ciclo de 7 dias, de lunes a domingo, donde cada "
            + "dia lleva un turno del catalogo por su id (miralo con buscar_turnos). Solo admite turnos "
            + "completos; el descanso es un turno mas. Para ciclos de otra longitud (quincenal, 8 dias) "
            + "usa crear_plantilla o crear_plantilla_rotativa. Recibe la composicion como JSON: una "
            + "entrada por dia con dia (lunes..domingo o 1..7) y turno_id o franja (HH:mm-HH:mm, "
            + "excluyentes; la franja crea o reutiliza un turno nombrado con la franja, ej. 07:00-17:00, "
            + "o 22:00-06:00+1 si cruza la medianoche; fin menor que inicio cruza la medianoche, "
            + "inicio igual a fin es 24 horas). Los dias que no listes quedan sin turno. Si algun turno "
            + "no existe, no crea nada y te dice cuales faltan. Los dias que el dominio rechace quedan "
            + "sin turno; corrigelos con asignar_turno_a_dia_de_plantilla (lunes es el dia 1, domingo el "
            + "7). El nombre es unico en el catalogo. Con jornada_id (miralo con listar_jornadas) la "
            + "plantilla nace asociada a esa Jornada. Aparece en listar_plantillas en unos segundos. "
            + "Para aplicarla usa aplicar_plantilla_semanal.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico de la plantilla semanal en el catalogo.", isRequired: true)]
        string nombre,
        [McpToolProperty(
            "dias",
            "Composicion de la plantilla como JSON: lista de objetos con dia (lunes..domingo o 1..7) y "
            + "turno_id (id que da buscar_turnos) o franja (HH:mm-HH:mm, excluyentes). Ejemplo: "
            + """[{"dia":"lunes","turno_id":"<id>"},{"dia":"martes","franja":"07:00-17:00"}].""",
            isRequired: true)]
        string dias,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.")]
        string? jornadaId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Format(Mensajes.CampoObligatorio, "nombre");

        if (string.IsNullOrWhiteSpace(dias))
            return string.Format(Mensajes.CampoObligatorio, "dias");

        List<DiaDePlantillaEntrada>? entradas;
        try
        {
            entradas = JsonSerializer.Deserialize<List<DiaDePlantillaEntrada>>(dias, OpcionesLectura);
        }
        catch (JsonException)
        {
            return Mensajes.DiasJsonInvalido;
        }

        if (entradas is null || entradas.Count == 0)
            return Mensajes.DiasVacio;

        var vistos = new HashSet<int>();
        var validadas = new List<AsignacionDeDia>();

        foreach (var entrada in entradas)
        {
            // Undefined (la entrada no trae la clave "dia") y cualquier otra forma que no sea
            // texto ni numero caen en string.Empty: GetRawText() sobre un JsonElement sin
            // inicializar lanza, y una tool nunca responde con excepcion (CA-ADR-0030).
            var textoDia = entrada.Dia.ValueKind switch
            {
                JsonValueKind.String => entrada.Dia.GetString() ?? string.Empty,
                JsonValueKind.Number => entrada.Dia.GetRawText(),
                _ => string.Empty
            };

            if (!DiaSemanaMcp.TryParsear(textoDia, out var diaIso))
                return string.Format(Mensajes.DiaDesconocido, textoDia);

            var semana = entrada.Semana ?? SemanaUnica;
            if (semana != SemanaUnica)
                return string.Format(Mensajes.SemanaDistintaDeUno, semana);

            if (!vistos.Add(diaIso))
                return string.Format(Mensajes.DiaDuplicado, semana, diaIso);

            var nombreDia = DiaSemanaMcp.NombreDe(diaIso);
            if (!string.IsNullOrWhiteSpace(entrada.Turno))
                return string.Format(Mensajes.TurnoPorNombreNoSoportado, semana, nombreDia);

            var traeTurno = !string.IsNullOrWhiteSpace(entrada.TurnoId);
            var traeFranja = !string.IsNullOrWhiteSpace(entrada.Franja);

            if (traeTurno && traeFranja)
                return string.Format(Mensajes.TurnoYFranjaExcluyentes, semana, nombreDia);
            if (!traeTurno && !traeFranja)
                return string.Format(Mensajes.TurnoOFranjaObligatorio, semana, nombreDia);

            if (traeTurno)
            {
                if (!Guid.TryParse(entrada.TurnoId, out var turnoGuid))
                    return string.Format(Mensajes.TurnoIdInvalido, entrada.TurnoId);
                validadas.Add(new AsignacionDeDia(diaIso, turnoGuid.ToString(), null));
                continue;
            }

            if (!FranjaInline.TryParsear(entrada.Franja!, out var inline))
                return string.Format(Mensajes.FranjaInvalida, semana, nombreDia, entrada.Franja);

            validadas.Add(new AsignacionDeDia(diaIso, null, inline));
        }

        Guid? jornadaGuid = null;
        if (!string.IsNullOrWhiteSpace(jornadaId))
        {
            if (!Guid.TryParse(jornadaId, out var parseado))
                return string.Format(Mensajes.JornadaIdInvalido, jornadaId);
            jornadaGuid = parseado;
        }

        var creacion = await new CreadorDePlantillaTurnos(programacion, resolutorPorId, resolutor).CrearAsync(
            nombre, DiasPorSemana, validadas, jornadaGuid,
            new MensajesDeCreacion(
                Mensajes.RechazoDelDominio,
                Mensajes.TurnosNoExisten,
                Mensajes.JornadaNoExiste,
                new MensajesDeTurnoEnLinea(
                    Mensajes.RechazoDelDominio, Mensajes.TurnoInlineEnConflicto, Mensajes.TurnoInlineNombreDuplicado,
                    Mensajes.FalloTurnoInline, Mensajes.NingunTurnoInlineCreado)),
            ct);
        if (creacion.Rechazo is { } rechazo)
            return rechazo;

        var creada = creacion.Plantilla!;
        return RespuestaJson.Serializar(new PlantillaCreadaResumen(
            Mensajes.ResultadoPlantillaCreada,
            new PlantillaResumen(creada.PlantillaId.ToString(), nombre),
            creada.DiasAsignados,
            creada.DiasRechazados.Count > 0
                ? [.. creada.DiasRechazados.Select(d => new DiaRechazado(DiaSemanaMcp.NombreDe(d.Dia), d.Turno, d.Motivo))]
                : null,
            creada.DiasAsignados == DiasPorSemana,
            Mensajes.NotaVisibilidadEventual,
            creada.TurnosEnLinea.Count > 0 ? creada.TurnosEnLinea : null,
            creada.Jornada));
    }
}

/// <summary>
/// Eco de crear_plantilla_semanal hacia el asistente: el 201 del POST y el 204 de cada PUT no traen
/// body util, asi que la plantilla se reconstruye con lo que entro a la tool. Completa = los 7 dias asignados.
/// </summary>
public sealed record PlantillaCreadaResumen(
    string Resultado,
    PlantillaResumen Plantilla,
    int DiasAsignados,
    IReadOnlyList<DiaRechazado>? DiasRechazados,
    bool Completa,
    string Nota,
    IReadOnlyList<TurnoInlineResumen>? TurnosInline = null,
    JornadaDescritaResumen? Jornada = null);

public sealed record PlantillaResumen(string Id, string Nombre);

/// <summary>Un dia (lunes..domingo) que el dominio rechazo al asignarle turno (409 turno incompleto/retirado, etc.).</summary>
public sealed record DiaRechazado(string Dia, string Turno, string Motivo);

/// <summary>
/// Una entrada del JSON de dias. Dia queda como JsonElement (no string) porque el modelo puede
/// escribirlo con o sin comillas ("miercoles" o 3): DiaSemanaMcp.TryParsear normaliza ambas formas
/// a partir del texto que esta tool extrae de aqui. Semana solo se tolera como 1.
/// </summary>
public sealed record DiaDePlantillaEntrada(
    int? Semana, JsonElement Dia, string? Turno, string? Franja = null,
    [property: JsonPropertyName("turno_id")] string? TurnoId = null);
