using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DesactivarJornada;

public partial class DesactivarJornadaTool(
    ProgramacionApi programacion, ColaboradoresApi colaboradores, TimeProvider? reloj = null)
{
    internal const string NombreTool = "desactivar_jornada";
    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    [Function("DesactivarJornada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Desactiva una Jornada del catalogo: deja de ofrecerse para asignar, pero lo ya programado "
            + "conserva su copia. No se puede desactivar si la usa algun colaborador vigente, si es la "
            + "predeterminada o si la usa una plantilla semanal; en ese caso hay que reasignar primero "
            + "(asignar_jornada, asignar_jornada_por_grupo, asignar_jornada_predeterminada).")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Id de la Jornada (el que da listar_jornadas).", isRequired: true)]
        string jornadaId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(jornadaId, out var id))
            return string.Format(Mensajes.JornadaIdInvalido, jornadaId);

        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(), ZonaBogota).DateTime);
        var respuestaFichas = await colaboradores.ListarFichas(hoy, null, [], take: null, ct, jornadaId: id);
        if (await respuestaFichas.LeerFalloAsync(ct) is { } falloLectura)
            return string.Format(Mensajes.RechazoDelDominio, falloLectura);

        var vigentes = (await respuestaFichas.Content
            .ReadFromJsonAsync<SobreDeLista<FichaColaborador>>(OpcionesLectura, ct))?.Elementos ?? [];
        if (vigentes.Count > 0)
            return FraseDeJornadaEnUso(vigentes);

        var respuesta = await programacion.DesactivarJornada(id, ct);
        return await respuesta.LeerFalloAsync(ct) is { } fallo
            ? string.Format(Mensajes.RechazoDelDominio, fallo)
            : Mensajes.ResultadoJornadaDesactivada;
    }

    private static string FraseDeJornadaEnUso(IReadOnlyList<FichaColaborador> vigentes)
    {
        var enUso = vigentes.Count == 1
            ? Mensajes.JornadaEnUsoUnColaborador
            : string.Format(Mensajes.JornadaEnUsoVariosColaboradores, vigentes.Count);

        var conTerminacion = vigentes.Count(f => f.VigenteHasta is not null);
        return conTerminacion switch
        {
            0 => enUso,
            1 => $"{enUso}. {Mensajes.UnaTerminacionRegistrada}",
            _ => $"{enUso}. {string.Format(Mensajes.VariasTerminacionesRegistradas, conTerminacion)}"
        };
    }
}
