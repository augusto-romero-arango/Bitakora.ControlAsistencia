using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ProgramacionApi(HttpClient http)
{
    public Task<HttpResponseMessage> ListarTurnos(CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> ObtenerTurno(string id, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> CrearTurno(Guid turnoId, string nombre, bool esDescanso, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> RetirarTurno(string id, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> AgregarFranja(string id, FranjaAAgregar franja, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> QuitarFranja(string id, TimeOnly franja, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> AgregarSubFranja(string id, SubFranjaAAgregar subFranja, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> QuitarSubFranja(string id, SubFranjaAQuitar subFranja, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<HttpResponseMessage> AsignarSedeAFranja(
        string id, SedeDeFranjaAAsignar sedeDeFranja, CancellationToken ct) =>
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
    public FranjaAAgregar(TimeOnly inicio, TimeOnly fin, int? diaOffsetFin, SedeProgramada? sede) =>
        throw new NotImplementedException();

    public string Inicio { get; }

    public string Fin { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DiaOffsetFin { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SedeProgramada? Sede { get; }
}

public sealed record SubFranjaAAgregar
{
    public SubFranjaAAgregar(TimeOnly franja, string tipo, TimeOnly inicio, TimeOnly fin) =>
        throw new NotImplementedException();

    public string Franja { get; }

    public string Tipo { get; }

    public string Inicio { get; }

    public string Fin { get; }
}

public sealed record SedeDeFranjaAAsignar
{
    public SedeDeFranjaAAsignar(TimeOnly franja, SedeProgramada? sede) =>
        throw new NotImplementedException();

    public string Franja { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SedeProgramada? Sede { get; }
}

public sealed record SubFranjaAQuitar
{
    public SubFranjaAQuitar(TimeOnly franja, string tipo, TimeOnly inicio) =>
        throw new NotImplementedException();

    public string Franja { get; }

    public string Tipo { get; }

    public string Inicio { get; }
}

public sealed record SedeProgramada(string Id, string Nombre, string? CentroDeCostos);
