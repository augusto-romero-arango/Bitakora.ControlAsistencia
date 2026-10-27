using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaSemanal;

// Cliente HTTP puro que envuelve CrearPlantillaSemanal (POST programacion/plantillas-semanales,
// paso 1) + N AsignarTurnoADia (PUT .../dias/{semana}/{dia}, paso 2, MEF-ADR-0043): todos los PUT
// tocan el mismo stream que el POST (concurrencia optimista de Marten -> 409 espurios en
// paralelo), por eso van secuenciales -- a diferencia de los POST de solicitar_programacion_turno,
// que si tocan streams distintos. Lee el detalle de cada turno_id distinto (GET
// programacion/turnos/{id}, CA-ADR-0039 decision 18) antes de escribir nada: si alguno falta, no
// crea nada (evita plantillas huerfanas). Cada rechazo por dia se traduce a texto y no detiene al resto (CA-ADR-0030): la
// plantilla queda incompleta y visible. Un dia puede traer una franja inline (CA-ADR-0034 decision
// 6): el turno se resuelve/crea en el catalogo con nombre derivado antes de escribir la plantilla;
// solo entonces se lee el catalogo, porque el nombre es la clave de unicidad del turno inline.
public partial class CrearPlantillaSemanalTool(
    ProgramacionApi programacion, ResolutorTurnoPorId resolutorPorId, ResolutorTurnoPorNombre resolutor)
{
    internal const string NombreTool = "crear_plantilla_semanal";
    internal const int MinimoSemanas = 1;
    internal const int MaximoSemanas = 6;
    internal const int DiasPorSemana = 7;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("CrearPlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea una plantilla semanal de turnos: un molde de 1 a 6 semanas, lunes a domingo, "
            + "donde cada dia lleva un turno del catalogo por su id (miralo con "
            + "buscar_turnos). Solo admite turnos completos; el descanso es un turno mas. Recibe "
            + "la composicion en dias como JSON: una entrada por dia con semana (opcional, 1 por "
            + "defecto), dia (lunes..domingo o 1..7) y turno_id (id que da buscar_turnos) o franja (HH:mm-HH:mm, "
            + "excluyentes; la franja crea o reutiliza un turno nombrado con la franja, ej. 07:00-17:00, "
            + "o 22:00-06:00+1 si cruza la medianoche; fin menor que inicio cruza la medianoche, "
            + "inicio igual a fin es 24 horas). Si algun turno no existe, no crea "
            + "nada y te dice cuales faltan. Los dias que el dominio rechace quedan sin turno y la "
            + "plantilla incompleta; corrigelos con asignar_turno_a_dia. El nombre es unico en el "
            + "catalogo. Con jornada_id (miralo con listar_jornadas) la plantilla nace asociada a esa "
            + "Jornada y se audita contra sus limites. Aparece en listar_plantillas_semanales en unos segundos.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico de la plantilla semanal en el catalogo.", isRequired: true)]
        string nombre,
        [McpToolProperty("semanas", "Numero de semanas de la plantilla (1 a 6). Por defecto 1.")]
        int? semanas,
        [McpToolProperty(
            "dias",
            "Composicion de la plantilla como JSON: lista de objetos con semana (opcional, 1 por "
            + "defecto), dia (lunes..domingo o 1..7) y turno_id (id que da buscar_turnos) o franja (HH:mm-HH:mm, excluyentes). Ejemplo: "
            + """[{"semana":1,"dia":"lunes","turno_id":"<id>"},{"semana":1,"dia":"martes","franja":"07:00-17:00"}].""",
            isRequired: true)]
        string dias,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.")]
        string? jornadaId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Format(Mensajes.CampoObligatorio, "nombre");

        var semanasValor = semanas ?? MinimoSemanas;
        if (semanasValor < MinimoSemanas || semanasValor > MaximoSemanas)
            return string.Format(Mensajes.SemanasFueraDeRango, semanasValor);

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

        var vistos = new HashSet<(int Semana, int Dia)>();
        var validadas = new List<EntradaValidada>();

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

            var semana = entrada.Semana ?? MinimoSemanas;
            if (semana < MinimoSemanas || semana > semanasValor)
                return string.Format(Mensajes.SemanaFueraDeRango, semana, semanasValor);

            if (!vistos.Add((semana, diaIso)))
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
                validadas.Add(new EntradaValidada(semana, diaIso, turnoGuid.ToString(), null));
                continue;
            }

            if (!FranjaInline.TryParsear(entrada.Franja!, out var inline))
                return string.Format(Mensajes.FranjaInvalida, semana, nombreDia, entrada.Franja);

            validadas.Add(new EntradaValidada(semana, diaIso, inline.Nombre, inline));
        }

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

        var turnoIdPorReferencia = new Dictionary<string, string>();
        var nombreDeTurnoPorId = new Dictionary<string, string>();
        var inexistentes = new List<string>();
        foreach (var idTurno in validadas.Where(v => v.Inline is null).Select(v => v.Referencia).Distinct())
        {
            var resolucionTurno = await resolutorPorId.ResolverAsync(Guid.Parse(idTurno), ct);
            if (resolucionTurno.FalloDeLectura is { } falloLectura)
                return string.Format(Mensajes.RechazoDelDominio, falloLectura);
            if (resolucionTurno.Ficha is { } fichaTurno)
            {
                turnoIdPorReferencia[idTurno] = fichaTurno.Id;
                nombreDeTurnoPorId[idTurno] = fichaTurno.Nombre;
            }
            else
                inexistentes.Add(idTurno);
        }

        if (inexistentes.Count > 0)
            return string.Format(Mensajes.TurnosNoExisten, string.Join(", ", inexistentes));

        var preparados = await new PreparadorDeTurnosEnLinea(programacion, resolutor).PrepararAsync(
            [.. validadas.Where(v => v.Inline is not null).Select(v => v.Inline!)],
            new MensajesDeTurnoEnLinea(
                Mensajes.RechazoDelDominio, Mensajes.TurnoInlineEnConflicto, Mensajes.TurnoInlineNombreDuplicado,
                Mensajes.FalloTurnoInline, Mensajes.NingunTurnoInlineCreado),
            ct);
        if (preparados.Rechazo is { } rechazoInline)
            return rechazoInline;

        foreach (var (nombreInline, idInline) in preparados.TurnoIdPorNombre)
            turnoIdPorReferencia[nombreInline] = idInline;
        var turnosInline = preparados.Resumen;

        var plantillaId = Guid.CreateVersion7();
        var respuestaPost = await programacion.CrearPlantillaSemanal(plantillaId, nombre, semanasValor, jornadaGuid, ct);
        if (await respuestaPost.LeerFalloAsync(ct) is { } falloPost)
            return string.Format(Mensajes.RechazoDelDominio, falloPost);

        var diasAsignados = 0;
        var diasRechazados = new List<DiaRechazado>();

        foreach (var validada in validadas)
        {
            var turnoId = turnoIdPorReferencia[validada.Referencia];
            var respuestaPut = await programacion.AsignarTurnoADia(
                plantillaId.ToString(), validada.Semana, validada.DiaIso, turnoId, ct);

            if (await respuestaPut.LeerFalloAsync(ct) is { } motivo)
                diasRechazados.Add(new DiaRechazado(
                    validada.Semana, DiaSemanaMcp.NombreDe(validada.DiaIso),
                    nombreDeTurnoPorId.GetValueOrDefault(validada.Referencia, validada.Referencia), motivo));
            else
                diasAsignados++;
        }

        return RespuestaJson.Serializar(new PlantillaCreadaResumen(
            Mensajes.ResultadoPlantillaCreada,
            new PlantillaResumen(plantillaId.ToString(), nombre, semanasValor),
            diasAsignados,
            diasRechazados.Count > 0 ? diasRechazados : null,
            diasAsignados == DiasPorSemana * semanasValor,
            Mensajes.NotaVisibilidadEventual,
            turnosInline.Count > 0 ? turnosInline : null,
            jornadaResumen));
    }

    // Referencia: el turno_id canonico, o el nombre derivado de la franja si el dia es inline.
    private sealed record EntradaValidada(int Semana, int DiaIso, string Referencia, FranjaInline? Inline);
}

/// <summary>
/// Eco de crear_plantilla_semanal hacia el asistente: el 201 del POST y el 204 de cada PUT no
/// traen body util, asi que la plantilla se reconstruye con lo que entro a la tool. DiasRechazados
/// null se omite del JSON (RespuestaJson, WhenWritingNull); completa se calcula localmente
/// (diasAsignados == DiasPorSemana * semanas): todo turno asignado es completo por construccion (#621).
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

public sealed record PlantillaResumen(string Id, string Nombre, int Semanas);

/// <summary>Un dia que el dominio rechazo al asignarle turno (409 turno incompleto/retirado, etc.).</summary>
public sealed record DiaRechazado(int Semana, string Dia, string Turno, string Motivo);

/// <summary>
/// Una entrada del JSON de dias. Dia queda como JsonElement (no string) porque el modelo puede
/// escribirlo con o sin comillas ("miercoles" o 3): DiaSemanaMcp.TryParsear normaliza ambas formas
/// a partir del texto que esta tool extrae de aqui.
/// </summary>
public sealed record DiaDePlantillaEntrada(
    int? Semana, JsonElement Dia, string? Turno, string? Franja = null,
    [property: JsonPropertyName("turno_id")] string? TurnoId = null);
