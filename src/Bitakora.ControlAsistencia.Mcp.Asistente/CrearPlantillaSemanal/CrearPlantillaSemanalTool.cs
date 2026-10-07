using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaSemanal;

// Cliente HTTP puro que envuelve CrearPlantillaSemanal (POST programacion/plantillas-semanales,
// paso 1) + N AsignarTurnoADia (PUT .../dias/{semana}/{dia}, paso 2, MEF-ADR-0043): todos los PUT
// tocan el mismo stream que el POST (concurrencia optimista de Marten -> 409 espurios en
// paralelo), por eso van secuenciales -- a diferencia de los POST de solicitar_programacion_turno,
// que si tocan streams distintos. Resuelve TODOS los nombres de turno con una sola lectura de GET
// programacion/turnos antes de escribir nada: si alguno falta, no crea nada (evita plantillas
// huerfanas). Cada rechazo por dia se traduce a texto y no detiene al resto (CA-ADR-0030): la
// plantilla queda incompleta y visible. Un dia puede traer una franja inline (CA-ADR-0034 decision
// 6): el turno se resuelve/crea en el catalogo con nombre derivado antes de escribir la plantilla.
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
            + "defecto), dia (lunes..domingo o 1..7) y turno (nombre exacto) o franja (HH:mm-HH:mm, "
            + "excluyentes; la franja crea o reutiliza un turno nombrado con la franja, ej. 07:00-17:00, "
            + "o 22:00-06:00+1 si cruza la medianoche; fin menor que inicio cruza la medianoche, "
            + "inicio igual a fin es 24 horas). Si algun turno no existe, no crea "
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
            + "defecto), dia (lunes..domingo o 1..7) y turno (nombre exacto del catalogo) o franja (HH:mm-HH:mm, excluyentes). Ejemplo: "
            + """[{"semana":1,"dia":"lunes","turno":"Cocina Manana"},{"semana":1,"dia":"martes","franja":"07:00-17:00"}].""",
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

            var nombreDia = DiaSemanaMcp.NombreDe(diaIso);
            var traeTurno = !string.IsNullOrWhiteSpace(entrada.Turno);
            var traeFranja = !string.IsNullOrWhiteSpace(entrada.Franja);

            if (traeTurno && traeFranja)
                return string.Format(Mensajes.TurnoYFranjaExcluyentes, semana, nombreDia);
            if (!traeTurno && !traeFranja)
                return string.Format(Mensajes.TurnoOFranjaObligatorio, semana, nombreDia);

            if (traeTurno)
            {
                validadas.Add(new EntradaValidada(semana, diaIso, entrada.Turno!, null));
                continue;
            }

            if (!FranjaInline.TryParsear(entrada.Franja!, out var inline))
                return string.Format(Mensajes.FranjaInvalida, semana, nombreDia, entrada.Franja);

            validadas.Add(new EntradaValidada(semana, diaIso, inline.Nombre, inline));
        }

        var resolucion = await resolutor.ResolverVariosAsync(validadas.Select(v => v.NombreTurno), ct);
        if (resolucion.FalloDeLectura is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);

        var resueltas = validadas.Zip(resolucion.Resoluciones, (v, r) => (Entrada: v, r.Ficha)).ToList();

        var faltantes = resueltas
            .Where(x => x.Entrada.Inline is null && x.Ficha is null)
            .Select(x => x.Entrada.NombreTurno)
            .Distinct()
            .ToList();
        if (faltantes.Count > 0)
            return string.Format(
                Mensajes.TurnosNoExisten,
                string.Join(", ", faltantes),
                string.Join(", ", resolucion.NombresDisponibles));

        var turnoIdPorNombre = resueltas
            .Where(x => x.Entrada.Inline is null)
            .DistinctBy(x => x.Entrada.NombreTurno)
            .ToDictionary(x => x.Entrada.NombreTurno, x => x.Ficha!.Id);

        var inlinePorNombre = resueltas
            .Where(x => x.Entrada.Inline is not null)
            .DistinctBy(x => x.Entrada.NombreTurno)
            .Select(x => (Inline: x.Entrada.Inline!, x.Ficha))
            .ToList();

        var clasificadas = inlinePorNombre
            .Select(x => (x.Inline, x.Ficha, Clase: Clasificar(x.Inline, x.Ficha)))
            .ToList();

        if (clasificadas.FirstOrDefault(x => x.Clase == ClaseInline.Distinto) is { Inline: not null } conflicto)
            return string.Format(Mensajes.TurnoInlineEnConflicto, conflicto.Inline.Nombre);

        var turnosInline = new List<TurnoInlineResumen>();
        var creados = new List<string>();

        foreach (var (inline, ficha, clase) in clasificadas)
        {
            var turnoId = ficha is null ? Guid.CreateVersion7().ToString() : ficha.Id;

            if (clase == ClaseInline.Equivalente)
            {
                turnosInline.Add(new TurnoInlineResumen(inline.Nombre, AccionReutilizo));
                turnoIdPorNombre[inline.Nombre] = turnoId;
                continue;
            }

            if (ficha is null)
            {
                var respuestaCrear = await programacion.CrearTurno(Guid.Parse(turnoId), inline.Nombre, false, ct);
                if (await respuestaCrear.LeerFalloAsync(ct) is { } falloCrear)
                    return respuestaCrear.StatusCode == System.Net.HttpStatusCode.Conflict
                        ? string.Format(Mensajes.TurnoInlineNombreDuplicado, inline.Nombre, ListarCreados(creados))
                        : string.Format(Mensajes.FalloTurnoInline, inline.Nombre, falloCrear, ListarCreados(creados));
                creados.Add(inline.Nombre);
            }

            var respuestaFranja = await programacion.AgregarFranja(
                turnoId, new FranjaAAgregar(inline.Inicio, inline.Fin, inline.DiaOffsetEnviado, null), ct);
            if (await respuestaFranja.LeerFalloAsync(ct) is { } falloFranja)
                return string.Format(Mensajes.FalloTurnoInline, inline.Nombre, falloFranja, ListarCreados(creados));

            turnosInline.Add(new TurnoInlineResumen(
                inline.Nombre, ficha is null ? AccionCreo : AccionCompleto));
            turnoIdPorNombre[inline.Nombre] = turnoId;
        }

        var plantillaId = Guid.CreateVersion7();
        var respuestaPost = await programacion.CrearPlantillaSemanal(plantillaId, nombre, semanasValor, ct);
        if (await respuestaPost.LeerFalloAsync(ct) is { } falloPost)
            return string.Format(Mensajes.RechazoDelDominio, falloPost);

        var diasAsignados = 0;
        var diasRechazados = new List<DiaRechazado>();

        foreach (var validada in validadas)
        {
            var turnoId = turnoIdPorNombre[validada.NombreTurno];
            var respuestaPut = await programacion.AsignarTurnoADia(
                plantillaId.ToString(), validada.Semana, validada.DiaIso, turnoId, ct);

            if (await respuestaPut.LeerFalloAsync(ct) is { } motivo)
                diasRechazados.Add(new DiaRechazado(
                    validada.Semana, DiaSemanaMcp.NombreDe(validada.DiaIso), validada.NombreTurno, motivo));
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
            turnosInline.Count > 0 ? turnosInline : null));
    }

    private const string AccionCreo = "creo";
    private const string AccionReutilizo = "reutilizo";
    private const string AccionCompleto = "completo";

    private static string ListarCreados(List<string> creados) =>
        creados.Count > 0 ? string.Join(", ", creados) : Mensajes.NingunTurnoInlineCreado;

    private enum ClaseInline { Nuevo, Equivalente, Vacio, Distinto }

    private static ClaseInline Clasificar(FranjaInline inline, FichaTurno? ficha) =>
        ficha switch
        {
            null => ClaseInline.Nuevo,
            { EsDescanso: true } => ClaseInline.Distinto,
            { Franjas.Count: 0 } => ClaseInline.Vacio,
            { Franjas: [var f] } when inline.Equivale(f) => ClaseInline.Equivalente,
            _ => ClaseInline.Distinto
        };

    private sealed record EntradaValidada(int Semana, int DiaIso, string NombreTurno, FranjaInline? Inline);

    private sealed record FranjaInline(TimeOnly Inicio, TimeOnly Fin)
    {
        public int? DiaOffsetEnviado => Inicio == Fin ? 1 : null;

        private int DiaOffsetFin => Fin <= Inicio ? 1 : 0;

        public string Nombre => NotacionFranja.Rango(Inicio, Fin, 0, DiaOffsetFin);

        public bool Equivale(FranjaFicha f) =>
            f.HoraInicio == Inicio && f.HoraFin == Fin && f.DiaOffsetFin == DiaOffsetFin
            && f.Descansos.Count == 0 && f.Extras.Count == 0 && f.SedeId is null;

        public static bool TryParsear(string texto, out FranjaInline inline)
        {
            inline = null!;
            var partes = texto.Split('-');
            if (partes.Length != 2
                || !NotacionFranja.TryParseHora(partes[0].Trim(), out var inicio)
                || !NotacionFranja.TryParseHora(partes[1].Trim(), out var fin))
                return false;

            inline = new FranjaInline(inicio, fin);
            return true;
        }
    }
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
    IReadOnlyList<TurnoInlineResumen>? TurnosInline = null);

/// <summary>Turno creado/reutilizado/completado por una entrada franja; accion: creo, reutilizo o completo.</summary>
public sealed record TurnoInlineResumen(string Nombre, string Accion);

public sealed record PlantillaResumen(string Id, string Nombre, int Semanas);

/// <summary>Un dia que el dominio rechazo al asignarle turno (409 turno incompleto/retirado, etc.).</summary>
public sealed record DiaRechazado(int Semana, string Dia, string Turno, string Motivo);

/// <summary>
/// Una entrada del JSON de dias. Dia queda como JsonElement (no string) porque el modelo puede
/// escribirlo con o sin comillas ("miercoles" o 3): DiaSemanaMcp.TryParsear normaliza ambas formas
/// a partir del texto que esta tool extrae de aqui.
/// </summary>
public sealed record DiaDePlantillaEntrada(int? Semana, JsonElement Dia, string? Turno, string? Franja = null);
