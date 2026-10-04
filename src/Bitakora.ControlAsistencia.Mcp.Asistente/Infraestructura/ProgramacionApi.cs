using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ProgramacionApi(HttpClient http)
{
    private static readonly HttpMethod Query = new("QUERY");

    public Task<HttpResponseMessage> ListarTurnos(CancellationToken ct) =>
        http.GetAsync("api/programacion/turnos", ct);

    public Task<HttpResponseMessage> ObtenerTurno(string id, CancellationToken ct) =>
        http.GetAsync($"api/programacion/turnos/{Uri.EscapeDataString(id)}", ct);

    public Task<HttpResponseMessage> CrearTurno(Guid turnoId, string nombre, bool esDescanso, CancellationToken ct) =>
        http.PostAsJsonAsync("api/programacion/turnos", new { turnoId, nombre, esDescanso }, ct);

    public Task<HttpResponseMessage> RetirarTurno(string id, CancellationToken ct) =>
        http.DeleteAsync($"api/programacion/turnos/{Uri.EscapeDataString(id)}", ct);

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

    public Task<HttpResponseMessage> AsignarSedeAFranja(
        string id, SedeDeFranjaAAsignar sedeDeFranja, CancellationToken ct) =>
        http.PostAsJsonAsync(
            $"api/programacion/turnos/{Uri.EscapeDataString(id)}:asignar-sede-franja", sedeDeFranja, ct);

    public Task<HttpResponseMessage> ListarPlantillasSemanales(CancellationToken ct) =>
        http.GetAsync("api/programacion/plantillas-semanales", ct);

    public Task<HttpResponseMessage> ObtenerPlantillaSemanal(string id, CancellationToken ct) =>
        http.GetAsync($"api/programacion/plantillas-semanales/{Uri.EscapeDataString(id)}", ct);

    public Task<HttpResponseMessage> CrearPlantillaSemanal(
        Guid plantillaId, string nombre, int semanas, CancellationToken ct) =>
        http.PostAsJsonAsync("api/programacion/plantillas-semanales", new { plantillaId, nombre, semanas }, ct);

    public Task<HttpResponseMessage> AsignarTurnoADia(
        string plantillaId, int semana, int dia, string turnoId, CancellationToken ct) =>
        http.PutAsJsonAsync(
            $"api/programacion/plantillas-semanales/{Uri.EscapeDataString(plantillaId)}/dias/{semana}/{dia}",
            new { turnoId },
            ct);

    public Task<HttpResponseMessage> RetirarPlantillaSemanal(string id, CancellationToken ct) =>
        http.DeleteAsync($"api/programacion/plantillas-semanales/{Uri.EscapeDataString(id)}", ct);

    public Task<HttpResponseMessage> QuitarTurnoDeDia(
        string plantillaId, int semana, int dia, CancellationToken ct) =>
        http.DeleteAsync(
            $"api/programacion/plantillas-semanales/{Uri.EscapeDataString(plantillaId)}/dias/{semana}/{dia}",
            ct);

    public Task<HttpResponseMessage> SolicitarProgramacion(
        SolicitudProgramacionTurno solicitud, CancellationToken ct) =>
        http.PostAsJsonAsync("api/programacion/solicitudes", solicitud, ct);

    public Task<HttpResponseMessage> ProgramarAusencia(
        string codigoColaborador, AusenciaAProgramar ausencia, CancellationToken ct) =>
        http.PostAsJsonAsync(
            $"api/programacion/colaboradores/{Uri.EscapeDataString(codigoColaborador)}/ausencias", ausencia, ct);

    public Task<HttpResponseMessage> ListarAusenciasColaborador(
        string codigoColaborador, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var request = new HttpRequestMessage(
            Query,
            $"api/programacion/colaboradores/{Uri.EscapeDataString(codigoColaborador)}/ausencias")
        {
            Content = JsonContent.Create(new { desde, hasta })
        };

        return http.SendAsync(request, ct);
    }

    public Task<HttpResponseMessage> CancelarAusencia(
        string codigoColaborador, string id, IReadOnlyList<DateOnly> fechas, CancellationToken ct) =>
        http.PostAsJsonAsync(
            $"api/programacion/colaboradores/{Uri.EscapeDataString(codigoColaborador)}/ausencias/{Uri.EscapeDataString(id)}:cancelar",
            new { fechas },
            ct);

    public Task<HttpResponseMessage> ListarAusenciasDelEquipo(
        DateOnly desde,
        DateOnly hasta,
        IReadOnlyList<string>? codigosColaborador,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(Query, "api/programacion/ausencias")
        {
            Content = JsonContent.Create(new
            {
                desde,
                hasta,
                colaboradores = codigosColaborador is { Count: > 0 } ? codigosColaborador : null
            })
        };

        return http.SendAsync(request, ct);
    }
}

public sealed record SolicitudProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    ColaboradorSolicitado Colaborador,
    IReadOnlyList<DateOnly> Fechas,
    SedeProgramada Sede);

public sealed record ColaboradorSolicitado(string Identificacion, string CodigoColaborador, string NombreCompleto);

public sealed record AusenciaAProgramar(
    Guid Id,
    string Identificacion,
    string NombreCompleto,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string Motivo);

public sealed record AusenciaListada(
    string Id,
    string Motivo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IReadOnlyList<TramoAusencia> TramosVigentes);

public sealed record TramoAusencia(DateOnly Desde, DateOnly Hasta);

public sealed record FichaTurno(
    string Id,
    string Nombre,
    bool EsDescanso,
    string HorarioResumido,
    IReadOnlyList<FranjaFicha> Franjas,
    string Descripcion,
    bool Completo);

public sealed record FranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<SubFranjaFicha> Descansos,
    IReadOnlyList<SubFranjaFicha> Extras,
    string? SedeId,
    string? NombreSede,
    string Descripcion);

public sealed record SubFranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin);

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

public sealed record SedeDeFranjaAAsignar
{
    public SedeDeFranjaAAsignar(TimeOnly franja, SedeProgramada? sede)
    {
        Franja = NotacionFranja.Hora(franja);
        Sede = sede;
    }

    public string Franja { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SedeProgramada? Sede { get; }
}

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

public sealed record SedeProgramada(string Id, string Nombre, string? CentroDeCostos);
