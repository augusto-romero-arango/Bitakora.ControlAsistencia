using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarTurno;

public partial class RetirarTurnoTool(ProgramacionApi programacion, ResolutorTurnoPorId resolutor)
{
    internal const string NombreTool = "retirar_turno";

    [Function("RetirarTurno")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Retira un turno del catalogo por su id (ubicalo con buscar_turnos): deja "
            + "de poder programarse y su nombre queda libre para reutilizarse. Los dias ya "
            + "programados con ese turno no cambian. Usalo tambien para descartar un turno "
            + "incompleto o un diseno a medias.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno_id", "Id del turno, tal como lo da buscar_turnos.", isRequired: true)]
        string turnoId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(turnoId, out var idDelTurno))
            return string.Format(Mensajes.TurnoIdInvalido, turnoId);

        var resolucion = await resolutor.ResolverAsync(idDelTurno, ct);
        if (resolucion.FalloDeLectura is { } falloTurno)
            return string.Format(Mensajes.RechazoDelDominio, falloTurno);
        if (resolucion.Ficha is not { } ficha)
            return string.Format(Mensajes.TurnoNoExiste, turnoId);

        var respuesta = await programacion.RetirarTurno(ficha.Id, ct);

        if (!respuesta.IsSuccessStatusCode)
            return string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(ct));

        return RespuestaJson.Serializar(new TurnoRetiradoResumen(
            Mensajes.ResultadoTurnoRetirado,
            new TurnoRetiradoEco(ficha.Id, ficha.Nombre),
            Mensajes.NotaVisibilidadEventual));
    }
}

/// <summary>El DELETE no trae body: el eco se reconstruye con la ficha leida del detalle.</summary>
public sealed record TurnoRetiradoResumen(string Resultado, TurnoRetiradoEco Turno, string Nota);

public sealed record TurnoRetiradoEco(string Id, string Nombre);
