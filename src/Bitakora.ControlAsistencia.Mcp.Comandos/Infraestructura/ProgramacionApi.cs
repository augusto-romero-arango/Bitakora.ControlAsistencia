using System.Net.Http.Json;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Programacion. Ver el comentario de ConfiguracionClientesHttp
/// para el criterio de devolver el HttpResponseMessage crudo (MEF-ADR-0047 decision 3): el manejo
/// de status y el remodelado pertenecen a cada tool, no a este cliente. Recreado tras su retiro en
/// #573 -- issue #589 vuelve a necesitar este dominio.
/// </summary>
public sealed class ProgramacionApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarTurnos(CancellationToken ct) =>
        http.GetAsync("api/programacion/turnos", ct);

    public Task<HttpResponseMessage> SolicitarProgramacion(
        SolicitudProgramacionTurno solicitud, CancellationToken ct) =>
        http.PostAsJsonAsync("api/programacion/solicitudes", solicitud, ct);
}

/// <summary>
/// Ficha de turno del catalogo tal como la devuelve GET programacion/turnos -- solo los campos que
/// esta tool consume (MEF-ADR-0047 decision 3: contrato propio, no el read model del dominio).
/// </summary>
public sealed record FichaTurno(string Id, string Nombre, bool EsDescanso);

/// <summary>
/// Payload propio de la tool hacia POST /api/programacion/solicitudes (MEF-ADR-0039 decision 6: el
/// comando nunca reusa un tipo de un ensamblado de eventos). Serializa a camelCase, contrato exacto
/// del comando SolicitarProgramacionTurno (MEF-ADR-0043).
/// </summary>
public sealed record SolicitudProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    ColaboradorSolicitado Colaborador,
    IReadOnlyList<DateOnly> Fechas,
    SedeProgramada Sede);

public sealed record ColaboradorSolicitado(string Identificacion, string CodigoColaborador, string NombreCompleto);

public sealed record SedeProgramada(string Id, string Nombre, string? CentroDeCostos);
