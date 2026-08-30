namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Sedes. Ver <see cref="ProgramacionApi"/> para el criterio
/// de devolver el HttpResponseMessage crudo. Pide siempre activa=true: el servidor de consultas
/// solo conversa sobre sedes asignables (decision de revision del PR #512).
/// </summary>
public sealed class SedesApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarFichasActivas(CancellationToken ct) =>
        http.GetAsync("api/sedes/fichas?activa=true", ct);
}
