using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.AgregarFranja;

// Cliente HTTP puro que envuelve GET programacion/turnos (resolver nombre -> id, via
// ResolutorTurnoPorNombre) + GET sedes/fichas/{codigo} (solo si viene codigo_sede) + POST
// programacion/turnos/{id}:agregar-franja (paso 4 MEF-ADR-0043). La tool nunca calcula offsets:
// inicio == fin es la unica excepcion (franja de 24h, se traduce a diaOffsetFin: 1). El
// 400/404/409 del POST se traduce a texto, nunca a excepcion (CA-ADR-0030).
public partial class AgregarFranjaTool(ProgramacionApi programacion, SedesApi sedes)
{
    internal const string NombreTool = "agregar_franja";

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);

    [Function("AgregarFranja")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Agrega una franja ordinaria (segmento continuo de trabajo) a un turno del catalogo, "
            + "indicado por su nombre exacto. Horas en formato HH:mm; si fin es menor que inicio la "
            + "franja cruza la medianoche; inicio igual a fin significa una franja de 24 horas. "
            + "Opcional codigo_sede (obtenlo con listar_sedes) para prearmar la sede de esa franja. "
            + "Una franja no puede solaparse con otra del mismo turno ni agregarse a un turno de "
            + "descanso. Con la primera franja el turno queda completo y se puede programar.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno", "Nombre exacto del turno del catalogo (miralo con listar_turnos).", isRequired: true)]
        string turno,
        [McpToolProperty("inicio", "Hora de inicio de la franja, formato HH:mm.", isRequired: true)]
        string inicio,
        [McpToolProperty("fin", "Hora de fin de la franja, formato HH:mm.", isRequired: true)]
        string fin,
        [McpToolProperty(
            "codigo_sede",
            "Codigo de la sede a prearmar en esta franja (opcional, miralo con listar_sedes).")]
        string? codigoSede,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(turno))
            return string.Format(Mensajes.CampoObligatorio, "turno");
        if (string.IsNullOrWhiteSpace(inicio))
            return string.Format(Mensajes.CampoObligatorio, "inicio");
        if (string.IsNullOrWhiteSpace(fin))
            return string.Format(Mensajes.CampoObligatorio, "fin");

        if (!TryParseHora(inicio, out var horaInicio))
            return string.Format(Mensajes.HoraInvalida, "inicio", inicio);
        if (!TryParseHora(fin, out var horaFin))
            return string.Format(Mensajes.HoraInvalida, "fin", fin);

        var resolucion = await resolutor.ResolverAsync(turno, ct);
        if (resolucion.FalloDeLectura is { } falloTurnos)
            return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        if (resolucion.Ficha is null)
            return string.Format(
                Mensajes.TurnoNoExiste, turno, string.Join(", ", resolucion.NombresDisponibles));
        var fichaTurno = resolucion.Ficha;

        SedeProgramada? sedeParaBody = null;
        string? nombreSedeParaEco = null;
        if (!string.IsNullOrWhiteSpace(codigoSede))
        {
            var respuestaSede = await sedes.ObtenerFicha(codigoSede, ct);
            if (respuestaSede.StatusCode == HttpStatusCode.NotFound)
                return string.Format(Mensajes.SedeNoExiste, codigoSede);
            if (await TraducirFalloDeLectura(respuestaSede, ct) is { } falloSede)
                return falloSede;

            var fichaSede = (await respuestaSede.Content.ReadFromJsonAsync<FichaSede>(OpcionesLectura, ct))!;
            if (!fichaSede.Activa)
                return string.Format(Mensajes.SedeInactiva, codigoSede);

            sedeParaBody = new SedeProgramada(fichaSede.Codigo, fichaSede.Nombre, fichaSede.CentroDeCostos);
            nombreSedeParaEco = fichaSede.Nombre;
        }

        // inicio == fin es la unica lectura valida de una franja de 24h -- se traduce a
        // diaOffsetFin: 1, nunca calculado por la tool a partir de otra cosa que no sea esta
        // igualdad (el dominio infiere el +1 por su cuenta cuando fin < inicio).
        int? diaOffsetFin = horaInicio == horaFin ? 1 : null;

        var body = ConstruirBody(horaInicio, horaFin, diaOffsetFin, sedeParaBody);
        var respuestaAgregar = await programacion.AgregarFranja(fichaTurno.Id, body, ct);
        if (await TraducirFalloDeLectura(respuestaAgregar, ct) is { } falloAgregar)
            return falloAgregar;

        var franja = NotacionFranja.Compactar(
            horaInicio, horaFin, diaOffsetFin ?? 0, [], [], nombreSedeParaEco);

        return RespuestaJson.Serializar(new FranjaAgregadaResumen(
            Mensajes.ResultadoFranjaAgregada, fichaTurno.Nombre, franja, Mensajes.NotaVisibilidadEventual));
    }

    private static object ConstruirBody(TimeOnly inicio, TimeOnly fin, int? diaOffsetFin, SedeProgramada? sede)
    {
        var body = new Dictionary<string, object>
        {
            ["inicio"] = inicio.ToString("HH:mm"),
            ["fin"] = fin.ToString("HH:mm"),
        };
        if (diaOffsetFin is not null)
            body["diaOffsetFin"] = diaOffsetFin.Value;
        if (sede is not null)
            body["sede"] = sede;

        return body;
    }

    private static bool TryParseHora(string valor, out TimeOnly hora) =>
        TimeOnly.TryParseExact(
            valor, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out hora);

    // El boundary del sistema (5xx, o cuerpo no JSON) se traduce a texto, nunca a excepcion
    // (CA-ADR-0030), con el status cuando el cuerpo viene vacio.
    private static async Task<string?> TraducirFalloDeLectura(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return null;

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        return string.Format(
            Mensajes.RechazoDelDominio,
            string.IsNullOrWhiteSpace(cuerpo) ? ((int)respuesta.StatusCode).ToString() : cuerpo);
    }
}

/// <summary>
/// Eco compacto de agregar_franja hacia el asistente: el 202 del dominio no trae body, asi que la
/// franja agregada se reconstruye con lo que se envio a la tool, en notacion compacta.
/// </summary>
public sealed record FranjaAgregadaResumen(string Resultado, string Turno, string Franja, string Nota);
