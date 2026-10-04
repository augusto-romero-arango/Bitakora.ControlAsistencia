namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class SedesApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarFichasActivas(CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> Registrar(
        string codigo, string nombre, string? ciudad, string? direccion, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> ObtenerFicha(string codigo, CancellationToken ct) =>
        throw new NotImplementedException();
}
