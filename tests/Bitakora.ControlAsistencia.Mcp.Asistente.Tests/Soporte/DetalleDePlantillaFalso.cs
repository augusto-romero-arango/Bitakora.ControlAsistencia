using System.Net;
using System.Text.Json.Nodes;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

public static class DetalleDePlantillaFalso
{
    public const string RutaCatalogo = "/api/programacion/plantillas-semanales";

    public static string RutaDetalle(string plantillaId) => $"{RutaCatalogo}/{plantillaId}";

    /// <summary>
    /// Registra GET programacion/plantillas-semanales/{id} con el elemento del catalogo fixture que
    /// tiene ese id (el detalle tiene la misma forma que un elemento del catalogo); sin elemento,
    /// responde 404. El catalogo (GET programacion/plantillas-semanales) no se registra: una tool que
    /// lo lea no encuentra ruta y el test falla.
    /// </summary>
    public static HandlerPorRuta RespondeDetalleDePlantilla(
        this HandlerPorRuta handler, string catalogoJson, string plantillaId)
    {
        var elemento = JsonNode.Parse(catalogoJson)!["elementos"]!.AsArray()
            .FirstOrDefault(e => e!["id"]!.GetValue<string>() == plantillaId);

        return elemento is null
            ? handler.Responde(HttpMethod.Get, RutaDetalle(plantillaId), HttpStatusCode.NotFound, "")
            : handler.Responde(HttpMethod.Get, RutaDetalle(plantillaId), HttpStatusCode.OK, elemento.ToJsonString());
    }
}
