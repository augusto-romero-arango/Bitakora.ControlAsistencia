using System.Net;
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
    internal const int MaximoDiasDelCiclo = 42;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("CrearPlantillaRotativa")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea una plantilla de turnos rotativa a partir de bloques de dias consecutivos con el mismo turno, "
            + "en orden: un 6x2 son 6 dias de un turno y 2 de descanso. Cada bloque lleva dias y turno_id "
            + "(miralo con buscar_turnos) o franja (HH:mm-HH:mm, excluyentes). Para aplicarla usa "
            + "aplicar_plantilla con fecha_inicio = el primer dia del primer bloque.")]
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

        var ciclo = validados.Sum(b => b.Dias);
        if (ciclo > MaximoDiasDelCiclo)
            return string.Format(Mensajes.SumaDeDiasFueraDeRango, ciclo);

        Guid? jornadaGuid = null;
        JornadaDescritaResumen? jornadaResumen = null;
        if (!string.IsNullOrWhiteSpace(jornadaId))
        {
            if (!Guid.TryParse(jornadaId, out var parseado))
                return string.Format(Mensajes.JornadaIdInvalido, jornadaId);

            var respuestaJornada = await programacion.ObtenerJornada(parseado, ct);
            if (respuestaJornada.StatusCode == HttpStatusCode.NotFound)
                return Mensajes.JornadaNoExiste;
            if (await respuestaJornada.LeerFalloAsync(ct) is { } falloJornada)
                return string.Format(Mensajes.RechazoDelDominio, falloJornada);

            jornadaGuid = parseado;
            jornadaResumen = (await respuestaJornada.LeerDescritaAsync(ct)).Resumir();
        }

        var asignaciones = new List<AsignacionDeDia>();
        var primerDiaDelBloque = new List<int>();
        foreach (var bloque in validados)
        {
            var primero = asignaciones.Count + 1;
            primerDiaDelBloque.Add(primero);
            asignaciones.AddRange(Enumerable.Range(primero, bloque.Dias)
                .Select(dia => new AsignacionDeDia(dia, bloque.TurnoId, bloque.Franja)));
        }

        var creacion = await new CreadorDePlantillaTurnos(programacion, resolutorPorId, resolutor).CrearAsync(
            nombre, ciclo, asignaciones, jornadaGuid,
            new MensajesDeCreacion(
                Mensajes.RechazoDelDominio,
                Mensajes.TurnosNoExisten,
                new MensajesDeTurnoEnLinea(
                    Mensajes.RechazoDelDominio, Mensajes.TurnoInlineEnConflicto, Mensajes.TurnoInlineNombreDuplicado,
                    Mensajes.FalloTurnoInline, Mensajes.NingunTurnoInlineCreado)),
            ct);
        if (creacion.Rechazo is { } rechazo)
            return rechazo;

        var creada = creacion.Plantilla!;
        var resumenDeBloques = validados
            .Select((b, i) => new BloqueResumen(
                b.Dias == 1 ? $"{primerDiaDelBloque[i]}" : $"{primerDiaDelBloque[i]}-{primerDiaDelBloque[i] + b.Dias - 1}",
                creada.TurnoPorDia[primerDiaDelBloque[i]]))
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
            jornadaResumen));
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
