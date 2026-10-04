using System.Net.Http.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class SedesApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarFichasActivas(CancellationToken ct) =>
        http.GetAsync("api/sedes/fichas?activa=true", ct);

    public Task<HttpResponseMessage> ListarFichas(CancellationToken ct) =>
        http.GetAsync("api/sedes/fichas", ct);

    public Task<HttpResponseMessage> Registrar(
        string codigo, string nombre, string? ciudad, string? direccion, CancellationToken ct) =>
        http.PostAsJsonAsync("api/sedes", new { codigo, nombre, ciudad, direccion }, ct);

    public Task<HttpResponseMessage> ObtenerFicha(string codigo, CancellationToken ct) =>
        http.GetAsync($"api/sedes/fichas/{Uri.EscapeDataString(codigo)}", ct);
}
