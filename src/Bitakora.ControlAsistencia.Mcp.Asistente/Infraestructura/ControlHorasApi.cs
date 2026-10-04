namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ControlHorasApi(HttpClient http)
{
    public Task<HttpResponseMessage> ConsultarTurnosVigentes(
        DateOnly desde,
        DateOnly hasta,
        string? codigoColaborador,
        string? sedeId,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
