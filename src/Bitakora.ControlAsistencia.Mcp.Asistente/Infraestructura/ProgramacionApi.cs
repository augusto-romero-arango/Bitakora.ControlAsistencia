using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ProgramacionApi(HttpClient http)
{
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
        throw new NotImplementedException();

    public Task<HttpResponseMessage> ObtenerPlantillaSemanal(string id, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> CrearPlantillaSemanal(
        Guid plantillaId, string nombre, int semanas, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> AsignarTurnoADia(
        string plantillaId, int semana, int dia, string turnoId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> RetirarPlantillaSemanal(string id, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> QuitarTurnoDeDia(
        string plantillaId, int semana, int dia, CancellationToken ct) =>
        throw new NotImplementedException();
}

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
