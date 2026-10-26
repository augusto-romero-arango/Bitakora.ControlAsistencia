using System.Net;
using System.Text.Json.Nodes;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

public static class DetalleDeTurnoFalso
{
    public const string RutaCatalogo = "/api/programacion/turnos";

    public static string RutaDetalle(string turnoId) => $"{RutaCatalogo}/{turnoId}";

    /// <summary>
    /// Registra GET programacion/turnos/{id} con el elemento del catalogo fixture que tiene ese id
    /// (el detalle tiene la misma forma que un elemento); sin elemento, responde 404 -- un turno
    /// retirado o inexistente no tiene detalle. El catalogo (GET programacion/turnos) no se registra:
    /// una tool que lo lea no encuentra ruta y el test falla.
    /// </summary>
    public static HandlerPorRuta RespondeDetalleDeTurno(
        this HandlerPorRuta handler, string catalogoJson, string turnoId)
    {
        var elemento = JsonNode.Parse(catalogoJson)!["elementos"]!.AsArray()
            .FirstOrDefault(e => e!["id"]!.GetValue<string>() == turnoId);

        return elemento is null
            ? handler.Responde(HttpMethod.Get, RutaDetalle(turnoId), HttpStatusCode.NotFound, "")
            : handler.Responde(HttpMethod.Get, RutaDetalle(turnoId), HttpStatusCode.OK, elemento.ToJsonString());
    }
}
