namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Programacion. Devuelve el HttpResponseMessage crudo: el
/// manejo de status (404 de ObtenerTurno incluido) y el remodelado pertenecen a cada tool, que es
/// quien define su contrato de respuesta al asistente.
/// </summary>
public sealed class ProgramacionApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarTurnos(CancellationToken ct) =>
        http.GetAsync("api/programacion/turnos", ct);

    public Task<HttpResponseMessage> ObtenerTurno(string id, CancellationToken ct) =>
        http.GetAsync($"api/programacion/turnos/{Uri.EscapeDataString(id)}", ct);

    public Task<HttpResponseMessage> ListarPlantillasSemanales(CancellationToken ct) =>
        http.GetAsync("api/programacion/plantillas-semanales", ct);

    public Task<HttpResponseMessage> ObtenerPlantillaSemanal(string id, CancellationToken ct) =>
        http.GetAsync($"api/programacion/plantillas-semanales/{Uri.EscapeDataString(id)}", ct);
}
