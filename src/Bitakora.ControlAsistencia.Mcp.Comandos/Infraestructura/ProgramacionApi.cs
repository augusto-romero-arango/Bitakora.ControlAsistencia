namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Programacion. Devuelve el HttpResponseMessage crudo: el
/// manejo de status y el remodelado pertenecen a cada tool (MEF-ADR-0047 decision 3).
/// </summary>
/// <remarks>
/// ListarElementos apunta al catalogo de turnos ya materializado por el dominio
/// (GET api/programacion/turnos, ListarFichasTurno) -- no un "api/programacion" generico que este
/// BC no expone -- para que la tool de ejemplo y sus smoke tests (MEF-ADR-0048 seccion 2)
/// ejerciten un endpoint real desde el primer deploy.
/// </remarks>
public sealed class ProgramacionApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarElementos(CancellationToken ct) =>
        http.GetAsync("api/programacion/turnos", ct);
}
