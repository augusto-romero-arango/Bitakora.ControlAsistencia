using System.Net.Http.Json;
using System.Text.Json.Serialization;

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

    // Acciones de negocio con verbo propio (paso 4 MEF-ADR-0043).
    public Task<HttpResponseMessage> AgregarFranja(string id, FranjaAAgregar franja, CancellationToken ct) =>
        http.PostAsJsonAsync($"api/programacion/turnos/{Uri.EscapeDataString(id)}:agregar-franja", franja, ct);

    public Task<HttpResponseMessage> QuitarFranja(string id, TimeOnly franja, CancellationToken ct) =>
        http.PostAsJsonAsync(
            $"api/programacion/turnos/{Uri.EscapeDataString(id)}:quitar-franja",
            new { franja = NotacionFranja.Hora(franja) },
            ct);

    public Task<HttpResponseMessage> AgregarSubFranja(string id, SubFranjaAAgregar subFranja, CancellationToken ct) =>
        http.PostAsJsonAsync(
            $"api/programacion/turnos/{Uri.EscapeDataString(id)}:agregar-subfranja", subFranja, ct);

    public Task<HttpResponseMessage> QuitarSubFranja(string id, SubFranjaAQuitar subFranja, CancellationToken ct) =>
        http.PostAsJsonAsync(
            $"api/programacion/turnos/{Uri.EscapeDataString(id)}:quitar-subfranja", subFranja, ct);

    // El body ya viene armado por la tool consumidora: con sede para asignar, sin la clave sede
    // (omitida, no null) para retirar (issue #611).
    public Task<HttpResponseMessage> AsignarSedeAFranja(string id, object body, CancellationToken ct) =>
        http.PostAsJsonAsync(
            $"api/programacion/turnos/{Uri.EscapeDataString(id)}:asignar-sede-franja", body, ct);
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
/// Payload propio de agregar_franja hacia POST programacion/turnos/{id}:agregar-franja. Las horas
/// viajan como HH:mm y no como la serializacion por defecto de TimeOnly (HH:mm:ss); diaOffsetFin y
/// sede se omiten del JSON cuando no aplican, en vez de viajar en null.
/// </summary>
public sealed record FranjaAAgregar
{
    public FranjaAAgregar(TimeOnly inicio, TimeOnly fin, int? diaOffsetFin, SedeProgramada? sede)
    {
        Inicio = NotacionFranja.Hora(inicio);
        Fin = NotacionFranja.Hora(fin);
        DiaOffsetFin = diaOffsetFin;
        Sede = sede;
    }

    public string Inicio { get; }

    public string Fin { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DiaOffsetFin { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SedeProgramada? Sede { get; }
}

/// <summary>
/// Payload propio de agregar_subfranja hacia POST programacion/turnos/{id}:agregar-subfranja. Las
/// horas viajan como HH:mm, igual que FranjaAAgregar; tipo llega ya normalizado por
/// TipoSubFranja.TryNormalizar, este record no lo revalida.
/// </summary>
public sealed record SubFranjaAAgregar
{
    public SubFranjaAAgregar(TimeOnly franja, string tipo, TimeOnly inicio, TimeOnly fin)
    {
        Franja = NotacionFranja.Hora(franja);
        Tipo = tipo;
        Inicio = NotacionFranja.Hora(inicio);
        Fin = NotacionFranja.Hora(fin);
    }

    public string Franja { get; }

    public string Tipo { get; }

    public string Inicio { get; }

    public string Fin { get; }
}

/// <summary>
/// Payload propio de quitar_subfranja hacia POST programacion/turnos/{id}:quitar-subfranja.
/// </summary>
public sealed record SubFranjaAQuitar
{
    public SubFranjaAQuitar(TimeOnly franja, string tipo, TimeOnly inicio)
    {
        Franja = NotacionFranja.Hora(franja);
        Tipo = tipo;
        Inicio = NotacionFranja.Hora(inicio);
    }

    public string Franja { get; }

    public string Tipo { get; }

    public string Inicio { get; }
}

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
