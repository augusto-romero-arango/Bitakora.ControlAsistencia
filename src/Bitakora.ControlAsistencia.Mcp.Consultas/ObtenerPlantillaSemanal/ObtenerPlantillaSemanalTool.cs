using System.Net;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerPlantillaSemanal;

// Tool de solo lectura sobre GET programacion/plantillas-semanales (resolver nombre -> id) + GET
// programacion/plantillas-semanales/{id} (issue #629). Remodela el cuadro compuesto de #625 como
// una linea por dia ("nombre (descripcion)") agrupada por semana con clave lunes..domingo, para
// que el asistente lo lea como tabla. El detalle con franjas de cada turno sigue siendo
// obtener_turno.
public partial class ObtenerPlantillaSemanalTool(ProgramacionApi api)
{
    internal const string NombreTool = "obtener_plantilla_semanal";

    private readonly ResolutorPlantillaPorNombre resolutor = new(api);

    [Function("ObtenerPlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Devuelve el cuadro de una plantilla semanal por su nombre exacto (mirala con "
            + "listar_plantillas_semanales): por cada semana, el turno de cada dia de lunes a "
            + "domingo con su nombre y horario, los dias sin turno y los turnos retirados o "
            + "incompletos. Para las franjas de un turno usa obtener_turno.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla",
            "Nombre exacto de la plantilla semanal del catalogo (mirala con listar_plantillas_semanales).",
            isRequired: true)]
        string plantilla,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plantilla))
            return string.Format(Mensajes.CampoObligatorio, "plantilla");

        var resolucion = await resolutor.ResolverAsync(plantilla, ct);
        if (resolucion.FalloDeLectura is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        if (resolucion.Cuadro is null)
            return string.Format(
                Mensajes.PlantillaNoExiste, plantilla, string.Join(", ", resolucion.NombresDisponibles));

        var respuesta = await api.ObtenerPlantillaSemanal(resolucion.Cuadro.Id, ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return string.Format(
                Mensajes.PlantillaNoExiste, plantilla, string.Join(", ", resolucion.NombresDisponibles));

        respuesta.EnsureSuccessStatusCode();

        var detalle = (await respuesta.Content.ReadFromJsonAsync<CuadroSemanalTurnos>(ct))!;

        var diasPorSemana = detalle.Dias.ToLookup(d => d.Semana);
        var cuadro = Enumerable.Range(1, detalle.Semanas)
            .Select(semana => ArmarSemana(semana, diasPorSemana[semana]))
            .ToList();

        return RespuestaJson.Serializar(new PlantillaSemanalDetallada(
            detalle.Id, detalle.Nombre.Trim(), detalle.Semanas, detalle.Completa, cuadro));
    }

    private static SemanaDelCuadro ArmarSemana(int semana, IEnumerable<DiaDelCuadro> dias)
    {
        // Indexado por el numero ISO de DiaDelCuadro.Dia; un dia ausente rinde "sin turno" (el
        // cuadro materializado omite los vacios, #624). Asignacion por indexador y no ToDictionary:
        // el upstream es un boundary y un (semana, dia) repetido no debe tumbar la tool call.
        var textos = new Dictionary<int, string>();
        foreach (var dia in dias)
            textos[dia.Dia] = TextoDelDia(dia.Turno);

        string Dia(int numeroIso) => textos.GetValueOrDefault(numeroIso, Mensajes.SinTurno);

        return new SemanaDelCuadro(
            semana, Dia(1), Dia(2), Dia(3), Dia(4), Dia(5), Dia(6), Dia(7));
    }

    // Retirado gana sobre incompleto: sin ficha en el catalogo, Completo llega false por
    // construccion (TurnoDelCuadroRespuesta.ResolverTurno de #625) y el motivo util es el retiro.
    private static string TextoDelDia(TurnoDelCuadro turno) => turno switch
    {
        { Retirado: true } => string.Format(Mensajes.TurnoRetirado, turno.Nombre ?? turno.Id),
        { Completo: false } => string.Format(Mensajes.TurnoIncompleto, turno.Nombre),
        _ => $"{turno.Nombre} {turno.Descripcion}"
    };
}

/// <summary>Contrato de respuesta de obtener_plantilla_semanal hacia el asistente (issue #629).</summary>
public sealed record PlantillaSemanalDetallada(
    string Id,
    string Nombre,
    int Semanas,
    bool Completa,
    IReadOnlyList<SemanaDelCuadro> Cuadro);

/// <summary>
/// Una semana del cuadro con una propiedad fija por dia para que el asistente la lea como tabla.
/// El orden de las propiedades ES el mapeo del numero ISO 8601 de DiaDelCuadro.Dia (1 = Lunes ..
/// 7 = Domingo) al nombre en espanol que pide el CA-2: RespuestaJson las serializa en minuscula
/// desde el nombre de cada propiedad, sin tabla de conversion intermedia.
/// </summary>
public sealed record SemanaDelCuadro(
    int Semana,
    string Lunes,
    string Martes,
    string Miercoles,
    string Jueves,
    string Viernes,
    string Sabado,
    string Domingo);
