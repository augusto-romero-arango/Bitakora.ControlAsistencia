using System.Text.Json;
using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaRotativa;

public partial class CrearPlantillaRotativaTool(
    ProgramacionApi programacion, ResolutorTurnoPorId resolutorPorId, ResolutorTurnoPorNombre resolutor)
{
    internal const string NombreTool = "crear_plantilla_rotativa";
    internal const int MaximoDiasDelCiclo = CrearPlantillaTool.MaximoDiasDelCiclo;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("CrearPlantillaRotativa")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea una plantilla de turnos rotativa a partir de bloques de dias consecutivos con el mismo turno, "
            + "en orden: un 6x2 son 6 dias de un turno y 2 de descanso; un 2-2-2-2 son 2 de manana, 2 de "
            + "tarde, 2 de noche y 2 de descanso. El ciclo dura la suma de los bloques (1 a 42 dias) y se "
            + "repite. Cada bloque lleva dias y turno_id (miralo con buscar_turnos) o franja (HH:mm-HH:mm, "
            + "excluyentes; la franja crea o reutiliza un turno nombrado con la franja, ej. 07:00-17:00, o "
            + "22:00-06:00+1 si cruza la medianoche). Solo admite turnos completos; el descanso es un turno "
            + "mas. Si algun turno no existe, no crea nada y te dice cuales faltan. Los dias que el dominio "
            + "rechace quedan sin turno; corrigelos con asignar_turno_a_dia_de_plantilla. El nombre es unico "
            + "en el catalogo. Con jornada_id (miralo con listar_jornadas) la plantilla nace asociada a esa "
            + "Jornada. Aparece en listar_plantillas en unos segundos. Para aplicarla usa aplicar_plantilla "
            + "con fecha_inicio = el primer dia del primer bloque.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico de la plantilla de turnos en el catalogo.", isRequired: true)]
        string nombre,
        [McpToolProperty(
            "bloques",
            "Bloques en orden como JSON: lista de objetos con dias y turno_id o franja. Ejemplo: "
            + """[{"dias":6,"turno_id":"<id>"},{"dias":2,"franja":"22:00-06:00"}].""",
            isRequired: true)]
        string bloques,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.")]
        string? jornadaId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Format(Mensajes.CampoObligatorio, "nombre");
        if (string.IsNullOrWhiteSpace(bloques))
            return string.Format(Mensajes.CampoObligatorio, "bloques");

        List<BloqueEntrada?>? entradas;
        try
        {
            entradas = JsonSerializer.Deserialize<List<BloqueEntrada?>>(bloques, OpcionesLectura);
        }
        catch (JsonException)
        {
            return Mensajes.BloquesJsonInvalido;
        }

        if (entradas is null || entradas.Count == 0)
            return Mensajes.BloquesVacio;

        var validados = new List<BloqueValidado>();
        for (var i = 0; i < entradas.Count; i++)
        {
            var numero = i + 1;
            var entrada = entradas[i];

            var textoDias = entrada?.Dias.ValueKind switch
            {
                JsonValueKind.String => entrada.Dias.GetString() ?? string.Empty,
                JsonValueKind.Number => entrada.Dias.GetRawText(),
                _ => string.Empty
            };
            if (!int.TryParse(textoDias, out var dias) || dias < 1)
                return string.Format(Mensajes.BloqueDiasInvalido, numero, textoDias);

            if (!string.IsNullOrWhiteSpace(entrada!.Turno))
                return string.Format(Mensajes.TurnoPorNombreNoSoportado, numero);

            var traeTurno = !string.IsNullOrWhiteSpace(entrada.TurnoId);
            var traeFranja = !string.IsNullOrWhiteSpace(entrada.Franja);
            if (traeTurno && traeFranja)
                return string.Format(Mensajes.TurnoYFranjaExcluyentes, numero);
            if (!traeTurno && !traeFranja)
                return string.Format(Mensajes.TurnoOFranjaObligatorio, numero);

            if (traeTurno)
            {
                if (!Guid.TryParse(entrada.TurnoId, out var turnoGuid))
                    return string.Format(Mensajes.TurnoIdInvalido, entrada.TurnoId);
                validados.Add(new BloqueValidado(dias, turnoGuid.ToString(), null));
                continue;
            }

            if (!FranjaInline.TryParsear(entrada.Franja!, out var inline))
                return string.Format(Mensajes.FranjaInvalida, numero, entrada.Franja);
            validados.Add(new BloqueValidado(dias, null, inline));
        }

        var suma = validados.Sum(b => (long)b.Dias);
        if (suma > MaximoDiasDelCiclo)
            return string.Format(Mensajes.SumaDeDiasFueraDeRango, suma);
        var ciclo = (int)suma;

        Guid? jornadaGuid = null;
        if (!string.IsNullOrWhiteSpace(jornadaId))
        {
            if (!Guid.TryParse(jornadaId, out var parseado))
                return string.Format(Mensajes.JornadaIdInvalido, jornadaId);
            jornadaGuid = parseado;
        }

        var asignaciones = new List<AsignacionDeDia>();
        var rangos = new List<(int Primero, int Ultimo)>();
        foreach (var bloque in validados)
        {
            var primero = asignaciones.Count + 1;
            asignaciones.AddRange(Enumerable.Range(primero, bloque.Dias)
                .Select(dia => new AsignacionDeDia(dia, bloque.TurnoId, bloque.Franja)));
            rangos.Add((primero, asignaciones.Count));
        }

        var creacion = await new CreadorDePlantillaTurnos(programacion, resolutorPorId, resolutor).CrearAsync(
            nombre, ciclo, asignaciones, jornadaGuid,
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
        var resumenDeBloques = rangos
            .Select(r => new BloqueResumen(
                r.Primero == r.Ultimo ? $"{r.Primero}" : $"{r.Primero}-{r.Ultimo}",
                creada.TurnoPorDia[r.Primero]))
            .ToList();

        return RespuestaJson.Serializar(new PlantillaRotativaCreadaResumen(
            Mensajes.ResultadoPlantillaCreada,
            new PlantillaTurnosResumen(creada.PlantillaId.ToString(), nombre, ciclo),
            resumenDeBloques,
            creada.DiasAsignados,
            creada.DiasRechazados.Count > 0 ? creada.DiasRechazados : null,
            creada.DiasAsignados == ciclo,
            Mensajes.NotaVisibilidadEventual,
            creada.TurnosEnLinea.Count > 0 ? creada.TurnosEnLinea : null,
            creada.Jornada));
    }

    private sealed record BloqueValidado(int Dias, string? TurnoId, FranjaInline? Franja);
}

/// <summary>Un bloque del JSON de entrada; Dias queda como JsonElement para tolerar 2 o "2" sin lanzar.</summary>
public sealed record BloqueEntrada(
    JsonElement Dias, string? Turno = null, string? Franja = null,
    [property: JsonPropertyName("turno_id")] string? TurnoId = null);

public sealed record PlantillaRotativaCreadaResumen(
    string Resultado,
    PlantillaTurnosResumen Plantilla,
    IReadOnlyList<BloqueResumen> Bloques,
    int DiasAsignados,
    IReadOnlyList<DiaDeCicloRechazado>? DiasRechazados,
    bool Completa,
    string Nota,
    IReadOnlyList<TurnoInlineResumen>? TurnosEnLinea = null,
    JornadaDescritaResumen? Jornada = null);

public sealed record BloqueResumen(string Dias, string Turno);
