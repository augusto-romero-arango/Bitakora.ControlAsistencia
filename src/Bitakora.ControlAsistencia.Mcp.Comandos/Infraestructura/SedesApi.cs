using System.Net.Http.Json;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Sedes. Ver el comentario de ConfiguracionClientesHttp para
/// el criterio de devolver el HttpResponseMessage crudo (MEF-ADR-0047 decision 3): el manejo de
/// status y el remodelado pertenecen a cada tool, no a este cliente.
/// </summary>
public sealed class SedesApi(HttpClient http)
{
    public Task<HttpResponseMessage> Registrar(
        string codigo, string nombre, string? ciudad, string? direccion, CancellationToken ct) =>
        http.PostAsJsonAsync("api/sedes", new { codigo, nombre, ciudad, direccion }, ct);

    public Task<HttpResponseMessage> ObtenerFicha(string codigo, CancellationToken ct) =>
        http.GetAsync($"api/sedes/fichas/{Uri.EscapeDataString(codigo)}", ct);
}

/// <summary>
/// Ficha de sede tal como la devuelve GET sedes/fichas/{codigo} -- solo los campos que esta tool
/// consume (MEF-ADR-0047 decision 3: contrato propio, no el read model del dominio).
/// </summary>
public sealed record FichaSede(string Codigo, string Nombre, string? CentroDeCostos, bool Activa);
