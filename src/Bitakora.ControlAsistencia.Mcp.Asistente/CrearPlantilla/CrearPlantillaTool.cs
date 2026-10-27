using System.Text.Json;
using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantilla;

// Valida la forma de la entrada y delega la escritura en CreadorDePlantillaTurnos.
public partial class CrearPlantillaTool(
    ProgramacionApi programacion, ResolutorTurnoPorId resolutorPorId, ResolutorTurnoPorNombre resolutor)
{
    internal const string NombreTool = "crear_plantilla";
    internal const int MinimoDiasDelCiclo = 1;
    internal const int MaximoDiasDelCiclo = 42;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("CrearPlantilla")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea una plantilla de turnos: un ciclo de N dias (1 a 42) que se repite, donde cada dia "
            + "lleva un turno del catalogo por su id (miralo con buscar_turnos). Solo admite turnos "
            + "completos; el descanso es un turno mas. Recibe el numero de dias del ciclo y la "
            + "composicion como JSON: una entrada por dia con dia (1..N) y turno_id o franja "
            + "(HH:mm-HH:mm, excluyentes; la franja crea o reutiliza un turno nombrado con la franja, "
            + "ej. 07:00-17:00, o 22:00-06:00+1 si cruza la medianoche; fin menor que inicio cruza la "
            + "medianoche, inicio igual a fin es 24 horas). Los dias que no listes quedan sin turno. "
            + "Si algun turno no existe, no crea nada y te dice cuales faltan. Los dias que el dominio "
            + "rechace quedan sin turno y la plantilla incompleta; corrigelos con "
            + "asignar_turno_a_dia_de_plantilla. El nombre es unico en el catalogo. Con jornada_id "
            + "(miralo con listar_jornadas) la plantilla nace asociada a esa Jornada y se audita "
            + "contra sus limites. Aparece en listar_plantillas en unos segundos. Para aplicarla usa "
            + "aplicar_plantilla.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico de la plantilla de turnos en el catalogo.", isRequired: true)]
        string nombre,
        [McpToolProperty("dias_del_ciclo", "Numero de dias del ciclo de la plantilla (1 a 42).", isRequired: true)]
        int? diasDelCiclo,
        [McpToolProperty(
            "dias",
            "Composicion de la plantilla como JSON: lista de objetos con dia (1..N) y turno_id (id que "
            + "da buscar_turnos) o franja (HH:mm-HH:mm, excluyentes). Ejemplo: "
            + """[{"dia":1,"turno_id":"<id>"},{"dia":2,"franja":"22:00-06:00"}].""",
            isRequired: true)]
        string dias,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.")]
        string? jornadaId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Format(Mensajes.CampoObligatorio, "nombre");

        if (diasDelCiclo is not { } ciclo)
            return string.Format(Mensajes.CampoObligatorio, "dias_del_ciclo");
        if (ciclo < MinimoDiasDelCiclo || ciclo > MaximoDiasDelCiclo)
            return string.Format(Mensajes.DiasDelCicloFueraDeRango, ciclo);

        if (string.IsNullOrWhiteSpace(dias))
            return string.Format(Mensajes.CampoObligatorio, "dias");

        List<DiaDeCicloEntrada>? entradas;
        try
        {
            entradas = JsonSerializer.Deserialize<List<DiaDeCicloEntrada>>(dias, OpcionesLectura);
        }
        catch (JsonException)
        {
            return Mensajes.DiasJsonInvalido;
        }

        if (entradas is null || entradas.Count == 0)
            return Mensajes.DiasVacio;

        var vistos = new HashSet<int>();
        var validadas = new List<EntradaValidada>();

        foreach (var entrada in entradas)
        {
            // Una entrada sin la clave "dia" (Undefined) cae en string.Empty: GetRawText() sobre un
            // JsonElement sin inicializar lanza, y una tool nunca responde con excepcion (CA-ADR-0030).
            var textoDia = entrada.Dia.ValueKind switch
            {
                JsonValueKind.String => entrada.Dia.GetString() ?? string.Empty,
                JsonValueKind.Number => entrada.Dia.GetRawText(),
                _ => string.Empty
            };

            if (!int.TryParse(textoDia, out var dia))
                return string.Format(Mensajes.DiaInvalido, textoDia);
            if (dia < MinimoDiasDelCiclo || dia > ciclo)
                return string.Format(Mensajes.DiaFueraDeRango, dia, ciclo);
            if (!vistos.Add(dia))
                return string.Format(Mensajes.DiaDuplicado, dia);

            if (!string.IsNullOrWhiteSpace(entrada.Turno))
                return string.Format(Mensajes.TurnoPorNombreNoSoportado, dia);

            var traeTurno = !string.IsNullOrWhiteSpace(entrada.TurnoId);
            var traeFranja = !string.IsNullOrWhiteSpace(entrada.Franja);

            if (traeTurno && traeFranja)
                return string.Format(Mensajes.TurnoYFranjaExcluyentes, dia);
            if (!traeTurno && !traeFranja)
                return string.Format(Mensajes.TurnoOFranjaObligatorio, dia);

            if (traeTurno)
            {
                if (!Guid.TryParse(entrada.TurnoId, out var turnoGuid))
                    return string.Format(Mensajes.TurnoIdInvalido, entrada.TurnoId);
                validadas.Add(new EntradaValidada(dia, turnoGuid.ToString(), null));
                continue;
            }

            if (!FranjaInline.TryParsear(entrada.Franja!, out var inline))
                return string.Format(Mensajes.FranjaInvalida, dia, entrada.Franja);

            validadas.Add(new EntradaValidada(dia, inline.Nombre, inline));
        }

        Guid? jornadaGuid = null;
        if (!string.IsNullOrWhiteSpace(jornadaId))
        {
            if (!Guid.TryParse(jornadaId, out var parseado))
                return string.Format(Mensajes.JornadaIdInvalido, jornadaId);
            jornadaGuid = parseado;
        }

        var creacion = await new CreadorDePlantillaTurnos(programacion, resolutorPorId, resolutor).CrearAsync(
            nombre, ciclo,
            [.. validadas.Select(v => new AsignacionDeDia(v.Dia, v.Inline is null ? v.Referencia : null, v.Inline))],
            jornadaGuid,
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
        return RespuestaJson.Serializar(new PlantillaTurnosCreadaResumen(
            Mensajes.ResultadoPlantillaCreada,
            new PlantillaTurnosResumen(creada.PlantillaId.ToString(), nombre, ciclo),
            creada.DiasAsignados,
            creada.DiasRechazados.Count > 0 ? creada.DiasRechazados : null,
            creada.DiasAsignados == ciclo,
            Mensajes.NotaVisibilidadEventual,
            creada.TurnosEnLinea.Count > 0 ? creada.TurnosEnLinea : null,
            creada.Jornada));
    }

    // Referencia: el turno_id canonico, o el nombre derivado de la franja si el dia es inline.
    private sealed record EntradaValidada(int Dia, string Referencia, FranjaInline? Inline);
}

/// <summary>
/// Eco de crear_plantilla hacia el asistente: el 201 del POST y el 204 de cada PUT no traen body
/// util, asi que la plantilla se reconstruye con lo que entro a la tool. Completa = N dias asignados.
/// </summary>
public sealed record PlantillaTurnosCreadaResumen(
    string Resultado,
    PlantillaTurnosResumen Plantilla,
    int DiasAsignados,
    IReadOnlyList<DiaDeCicloRechazado>? DiasRechazados,
    bool Completa,
    string Nota,
    IReadOnlyList<TurnoInlineResumen>? TurnosInline = null,
    JornadaDescritaResumen? Jornada = null);

public sealed record PlantillaTurnosResumen(string Id, string Nombre, int DiasDelCiclo);

/// <summary>Un dia que el dominio rechazo al asignarle turno (409 turno incompleto/retirado, etc.).</summary>
public sealed record DiaDeCicloRechazado(int Dia, string Turno, string Motivo);

/// <summary>Una entrada del JSON de dias; Dia queda como JsonElement para tolerar 2 o "2" sin lanzar.</summary>
public sealed record DiaDeCicloEntrada(
    JsonElement Dia, string? Turno, string? Franja = null,
    [property: JsonPropertyName("turno_id")] string? TurnoId = null);
