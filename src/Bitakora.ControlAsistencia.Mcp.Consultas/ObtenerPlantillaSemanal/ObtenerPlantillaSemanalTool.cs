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
        var textos = Enumerable.Range(1, 7).ToDictionary(DiaSemanaTexto.NombreDe, _ => Mensajes.SinTurno);

        foreach (var dia in dias)
            textos[DiaSemanaTexto.NombreDe(dia.Dia)] = TextoDelDia(dia.Turno);

        return new SemanaDelCuadro(
            semana,
            textos[DiaSemanaTexto.NombreDe(1)],
            textos[DiaSemanaTexto.NombreDe(2)],
            textos[DiaSemanaTexto.NombreDe(3)],
            textos[DiaSemanaTexto.NombreDe(4)],
            textos[DiaSemanaTexto.NombreDe(5)],
            textos[DiaSemanaTexto.NombreDe(6)],
            textos[DiaSemanaTexto.NombreDe(7)]);
    }

    private static string TextoDelDia(TurnoDelCuadro turno)
    {
        if (turno.Retirado)
            return string.Format(Mensajes.TurnoRetirado, turno.Nombre ?? turno.Id);

        if (!turno.Completo)
            return string.Format(Mensajes.TurnoIncompleto, turno.Nombre);

        return $"{turno.Nombre} {turno.Descripcion}";
    }
}

/// <summary>Contrato de respuesta de obtener_plantilla_semanal hacia el asistente (issue #629).</summary>
public sealed record PlantillaSemanalDetallada(
    string Id,
    string Nombre,
    int Semanas,
    bool Completa,
    IReadOnlyList<SemanaDelCuadro> Cuadro);

/// <summary>
/// Una semana del cuadro con una propiedad fija por dia (lunes..domingo, serializadas en
/// minuscula por RespuestaJson) para que el asistente la lea como tabla.
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
