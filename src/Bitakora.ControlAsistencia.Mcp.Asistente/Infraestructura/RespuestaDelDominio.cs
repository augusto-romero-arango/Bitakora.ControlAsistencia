namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// Un 5xx o un cuerpo inesperado llegaria como excepcion cruda a la tool call. Devuelve el motivo crudo
// (el cuerpo, o el status si viene vacio: un 503 de un Function App frio no trae body) y cada tool lo
// formatea con su RechazoDelDominio.
internal static class RespuestaDelDominio
{
    public static async Task<string?> LeerFalloAsync(this HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return null;

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(cuerpo) ? ((int)respuesta.StatusCode).ToString() : cuerpo;
    }
}
