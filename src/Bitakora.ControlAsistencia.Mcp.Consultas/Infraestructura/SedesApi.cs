namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Sedes. Ver <see cref="ProgramacionApi"/> para el criterio
/// de devolver el HttpResponseMessage crudo.
/// </summary>
public sealed class SedesApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarFichas(CancellationToken ct) =>
        http.GetAsync("api/sedes/fichas", ct);
}
