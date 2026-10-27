using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarTurno;

// Cliente HTTP puro de GET programacion/turnos/{id} (ResolutorTurnoPorId, CA-ADR-0039 decision 18)
// + DELETE programacion/turnos/{id} (paso 3 MEF-ADR-0043). El 404/409 del DELETE se devuelve como
// texto, nunca como excepcion (CA-ADR-0030).
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

/// <summary>
/// Eco compacto de retirar_turno hacia el asistente: el DELETE no trae body, asi que el turno
/// retirado se reconstruye con la ficha leida del detalle.
/// </summary>
public sealed record TurnoRetiradoResumen(string Resultado, TurnoRetiradoEco Turno, string Nota);

public sealed record TurnoRetiradoEco(string Id, string Nombre);
