using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.CrearPlantillaSemanal;

// Cliente HTTP puro que envuelve CrearPlantillaSemanal (POST programacion/plantillas-semanales,
// paso 1) + N AsignarTurnoADia (PUT .../dias/{semana}/{dia}, paso 2, MEF-ADR-0043): todos los PUT
// tocan el mismo stream que el POST (concurrencia optimista de Marten -> 409 espurios en
// paralelo), por eso van secuenciales -- a diferencia de los POST de solicitar_programacion_turno,
// que si tocan streams distintos. Resuelve TODOS los nombres de turno con una sola lectura de GET
// programacion/turnos antes de escribir nada: si alguno falta, no crea nada (evita plantillas
// huerfanas). Cada rechazo por dia se traduce a texto y no detiene al resto (CA-ADR-0030): la
// plantilla queda incompleta y visible. El turno descrito inline se separo a #651.
public partial class CrearPlantillaSemanalTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "crear_plantilla_semanal";
    internal const int MinimoSemanas = 1;
    internal const int MaximoSemanas = 6;
    internal const int DiasPorSemana = 7;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);

    [Function("CrearPlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea una plantilla semanal de turnos: un molde de 1 a 6 semanas, lunes a domingo, "
            + "donde cada dia lleva un turno del catalogo por su nombre exacto (miralo con "
            + "listar_turnos). Solo admite turnos completos; el descanso es un turno mas. Recibe "
            + "la composicion en dias como JSON: una entrada por dia con semana (opcional, 1 por "
            + "defecto), dia (lunes..domingo o 1..7) y turno. Si algun turno no existe, no crea "
            + "nada y te dice cuales faltan. Los dias que el dominio rechace quedan sin turno y la "
            + "plantilla incompleta; corrigelos con asignar_turno_a_dia. El nombre es unico en el "
            + "catalogo. Aparece en listar_plantillas_semanales en unos segundos.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico de la plantilla semanal en el catalogo.", isRequired: true)]
        string nombre,
        [McpToolProperty("semanas", "Numero de semanas de la plantilla (1 a 6). Por defecto 1.")]
        int? semanas,
        [McpToolProperty(
            "dias",
            "Composicion de la plantilla como JSON: lista de objetos con semana (opcional, 1 por "
            + "defecto), dia (lunes..domingo o 1..7) y turno (nombre exacto del catalogo). Ejemplo: "
            + """[{"semana":1,"dia":"lunes","turno":"Cocina Manana"}].""",
            isRequired: true)]
        string dias,
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

            if (string.IsNullOrWhiteSpace(entrada.Turno))
                return string.Format(
                    Mensajes.TurnoObligatorioEnEntrada, semana, DiaSemanaMcp.NombreDe(diaIso));

            validadas.Add(new EntradaValidada(semana, diaIso, entrada.Turno));
        }

        var resolucion = await resolutor.ResolverVariosAsync(validadas.Select(v => v.TurnoNombre), ct);
        if (resolucion.FalloDeLectura is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);

        var faltantes = resolucion.Resoluciones
            .Where(r => r.Ficha is null)
            .Select(r => r.NombreSolicitado)
            .Distinct()
            .ToList();
        if (faltantes.Count > 0)
            return string.Format(
                Mensajes.TurnosNoExisten,
                string.Join(", ", faltantes),
                string.Join(", ", resolucion.NombresDisponibles));

        var turnoIdPorNombre = resolucion.Resoluciones
            .DistinctBy(r => r.NombreSolicitado)
            .ToDictionary(r => r.NombreSolicitado, r => r.Ficha!.Id);

        var plantillaId = Guid.CreateVersion7();
        var respuestaPost = await programacion.CrearPlantillaSemanal(plantillaId, nombre, semanasValor, ct);
        if (await respuestaPost.LeerFalloAsync(ct) is { } falloPost)
            return string.Format(Mensajes.RechazoDelDominio, falloPost);

        var diasAsignados = 0;
        var diasRechazados = new List<DiaRechazado>();

        foreach (var validada in validadas)
        {
            var turnoId = turnoIdPorNombre[validada.TurnoNombre];
            var respuestaPut = await programacion.AsignarTurnoADia(
                plantillaId.ToString(), validada.Semana, validada.DiaIso, turnoId, ct);

            if (await respuestaPut.LeerFalloAsync(ct) is { } motivo)
                diasRechazados.Add(new DiaRechazado(
                    validada.Semana, DiaSemanaMcp.NombreDe(validada.DiaIso), validada.TurnoNombre, motivo));
            else
                diasAsignados++;
        }

        return RespuestaJson.Serializar(new PlantillaCreadaResumen(
            Mensajes.ResultadoPlantillaCreada,
            new PlantillaResumen(plantillaId.ToString(), nombre, semanasValor),
            diasAsignados,
            diasRechazados.Count > 0 ? diasRechazados : null,
            diasAsignados == DiasPorSemana * semanasValor,
            Mensajes.NotaVisibilidadEventual));
    }

    private sealed record EntradaValidada(int Semana, int DiaIso, string TurnoNombre);
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
    string Nota);

public sealed record PlantillaResumen(string Id, string Nombre, int Semanas);

/// <summary>Un dia que el dominio rechazo al asignarle turno (409 turno incompleto/retirado, etc.).</summary>
public sealed record DiaRechazado(int Semana, string Dia, string Turno, string Motivo);

/// <summary>
/// Una entrada del JSON de dias. Dia queda como JsonElement (no string) porque el modelo puede
/// escribirlo con o sin comillas ("miercoles" o 3): DiaSemanaMcp.TryParsear normaliza ambas formas
/// a partir del texto que esta tool extrae de aqui.
/// </summary>
public sealed record DiaDePlantillaEntrada(int? Semana, JsonElement Dia, string? Turno);
