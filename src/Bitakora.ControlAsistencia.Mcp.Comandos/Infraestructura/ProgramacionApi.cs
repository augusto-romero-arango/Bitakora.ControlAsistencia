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

    public Task<HttpResponseMessage> CrearTurno(Guid turnoId, string nombre, bool esDescanso, CancellationToken ct) =>
        http.PostAsJsonAsync("api/programacion/turnos", new { turnoId, nombre, esDescanso }, ct);

    public Task<HttpResponseMessage> RetirarTurno(string id, CancellationToken ct) =>
        http.DeleteAsync($"api/programacion/turnos/{Uri.EscapeDataString(id)}", ct);

    // Accion de negocio con verbo propio (paso 4 MEF-ADR-0043): el body ya viene armado por la
    // tool consumidora -- agregar_franja decide si incluye diaOffsetFin/sede (issue #609).
    public Task<HttpResponseMessage> AgregarFranja(string id, object body, CancellationToken ct) =>
        http.PostAsJsonAsync($"api/programacion/turnos/{Uri.EscapeDataString(id)}:agregar-franja", body, ct);

    // La franja se identifica por su hora de inicio en formato HH:mm -- igual que el body que el
    // dominio espera (QuitarFranjaBody), nunca la serializacion TimeOnly por defecto.
    public Task<HttpResponseMessage> QuitarFranja(string id, TimeOnly franja, CancellationToken ct) =>
        http.PostAsJsonAsync(
            $"api/programacion/turnos/{Uri.EscapeDataString(id)}:quitar-franja",
            new { franja = franja.ToString("HH:mm") },
            ct);
}

/// <summary>
/// Ficha de turno del catalogo tal como la devuelve GET programacion/turnos -- solo los campos que
/// las tools de este servidor consumen (MEF-ADR-0047 decision 3: contrato propio, no el read
/// model del dominio). Franjas crecio en el issue #609 para el eco de quitar_franja.
/// </summary>
public sealed record FichaTurno(
    string Id,
    string Nombre,
    bool EsDescanso,
    IReadOnlyList<FranjaFicha> Franjas);

/// <summary>Espejo parcial de FranjaFicha del read model -- issue #609 (eco de quitar_franja).</summary>
public sealed record FranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<SubFranjaFicha> Descansos,
    IReadOnlyList<SubFranjaFicha> Extras,
    string? SedeId,
    string? NombreSede);

public sealed record SubFranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin);

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
