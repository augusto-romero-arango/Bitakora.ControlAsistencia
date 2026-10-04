namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Programacion. Agrega aqui los metodos GET que las tools
/// de este servidor necesiten consumir de este dominio.
/// </summary>
public sealed class ProgramacionApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarElementos(CancellationToken ct) =>
        http.GetAsync("api/programacion/turnos", ct);
}
