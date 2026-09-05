namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Toda llamada a una Function App del BC es el boundary del sistema: un 5xx -- o un cuerpo que no
// es el JSON esperado -- llegaria como excepcion cruda a la tool call. Devuelve el motivo en crudo
// (el cuerpo, o el status cuando viene vacio: un 503 de un Function App frio no trae body) y cada
// tool lo formatea con su propio .resx RechazoDelDominio (CA-ADR-0030, MEF-ADR-0009).
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
