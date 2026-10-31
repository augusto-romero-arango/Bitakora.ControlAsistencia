using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ProgramacionApi(HttpClient http)
{
    private static readonly HttpMethod Query = new("QUERY");

    public Task<HttpResponseMessage> ListarTurnos(
        CancellationToken ct, int? take = null, string? cursor = null, string? sedeId = null) =>
        http.GetAsync(ConsultaDeLista("api/programacion/turnos", take, cursor, sedeId), ct);

    public Task<HttpResponseMessage> ObtenerTurno(string id, CancellationToken ct) =>
        http.GetAsync($"api/programacion/turnos/{Uri.EscapeDataString(id)}", ct);

    public Task<HttpResponseMessage> CrearTurno(
        Guid turnoId,
        string nombre,
        bool esDescanso,
        CancellationToken ct,
        IReadOnlyList<OrdinariaACrear>? ordinarias = null) =>
        http.PostAsJsonAsync(
            "api/programacion/turnos", new TurnoACrear(turnoId, nombre, esDescanso, ordinarias), ct);

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

    public Task<HttpResponseMessage> ListarJornadas(int take, string? cursor, CancellationToken ct) =>
        http.GetAsync(
            cursor is null
                ? $"api/programacion/jornadas?take={take}"
                : $"api/programacion/jornadas?take={take}&cursor={Uri.EscapeDataString(cursor)}",
            ct);

    public Task<HttpResponseMessage> ListarJornadasCompleto(CancellationToken ct) =>
        http.GetAsync("api/programacion/jornadas", ct);

    public Task<HttpResponseMessage> ObtenerJornada(Guid id, CancellationToken ct) =>
        http.GetAsync($"api/programacion/jornadas/{id}", ct);

    public Task<HttpResponseMessage> CrearJornada(JornadaACrear jornada, CancellationToken ct) =>
        http.PostAsJsonAsync("api/programacion/jornadas", jornada, ct);

    public Task<HttpResponseMessage> ModificarLimitesJornada(
        Guid id, LimitesJornadaAEnviar limites, CancellationToken ct) =>
        http.PutAsJsonAsync($"api/programacion/jornadas/{id}/limites", limites, ct);

    public Task<HttpResponseMessage> ModificarVentanaDeEntrada(
        string id, HorasYMinutosJornada amplitud, CancellationToken ct) =>
        http.PutAsJsonAsync($"api/programacion/turnos/{id}/ventana-entrada", new { amplitud }, ct);

    public Task<HttpResponseMessage> CrearTurnoDeHorarioAbierto(
        Guid turnoId, string nombre, HorasYMinutosJornada duracion, CancellationToken ct) =>
        http.PostAsJsonAsync("api/programacion/turnos/horario-abierto", new { turnoId, nombre, duracion }, ct);

    public Task<HttpResponseMessage> DesactivarJornada(Guid id, CancellationToken ct) =>
        http.PostAsync($"api/programacion/jornadas/{id}:desactivar", null, ct);

    public Task<HttpResponseMessage> AsignarJornadaPredeterminada(Guid jornadaId, CancellationToken ct) =>
        http.PutAsJsonAsync("api/programacion/preferencias/jornada-predeterminada", new { jornadaId }, ct);

    public Task<HttpResponseMessage> ListarPlantillasTurnos(
        CancellationToken ct, int? take = null, string? cursor = null) =>
        http.GetAsync(ConsultaDeLista("api/programacion/plantillas-turnos", take, cursor), ct);

    public Task<HttpResponseMessage> ObtenerPlantillaTurnos(string id, CancellationToken ct) =>
        http.GetAsync($"api/programacion/plantillas-turnos/{Uri.EscapeDataString(id)}", ct);

    public Task<HttpResponseMessage> CrearPlantillaTurnos(
        Guid plantillaId, string nombre, int diasDelCiclo, Guid? jornadaId, CancellationToken ct) =>
        http.PostAsJsonAsync("api/programacion/plantillas-turnos", new { plantillaId, nombre, diasDelCiclo, jornadaId }, ct);

    public Task<HttpResponseMessage> AsignarTurnoADiaDePlantillaTurnos(
        string plantillaId, int dia, string turnoId, CancellationToken ct) =>
        http.PutAsJsonAsync(
            $"api/programacion/plantillas-turnos/{Uri.EscapeDataString(plantillaId)}/dias/{dia}",
            new { turnoId },
            ct);

    public Task<HttpResponseMessage> AsignarJornadaAPlantillaTurnos(
        string plantillaId, Guid jornadaId, CancellationToken ct) =>
        http.PutAsJsonAsync(
            $"api/programacion/plantillas-turnos/{Uri.EscapeDataString(plantillaId)}/jornada",
            new { jornadaId },
            ct);

    public Task<HttpResponseMessage> QuitarJornadaDePlantillaTurnos(string plantillaId, CancellationToken ct) =>
        http.DeleteAsync($"api/programacion/plantillas-turnos/{Uri.EscapeDataString(plantillaId)}/jornada", ct);

    public Task<HttpResponseMessage> RetirarPlantillaTurnos(string id, CancellationToken ct) =>
        http.DeleteAsync($"api/programacion/plantillas-turnos/{Uri.EscapeDataString(id)}", ct);

    public Task<HttpResponseMessage> SolicitarProgramacion(
        SolicitudProgramacionTurno solicitud, CancellationToken ct) =>
        http.PostAsJsonAsync("api/programacion/solicitudes-en-fechas", solicitud, ct);

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
        int? take,
        string? cursor,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(Query, "api/programacion/ausencias")
        {
            Content = JsonContent.Create(new
            {
                desde,
                hasta,
                colaboradores = codigosColaborador is { Count: > 0 } ? codigosColaborador : null,
                take,
                cursor
            })
        };

        return http.SendAsync(request, ct);
    }

    private static string ConsultaDeLista(string ruta, int? take, string? cursor, string? sedeId = null)
    {
        var parametros = new List<string>();
        if (take is { } tope)
            parametros.Add($"take={tope}");
        if (cursor is not null)
            parametros.Add($"cursor={Uri.EscapeDataString(cursor)}");
        if (sedeId is not null)
            parametros.Add($"sedeId={Uri.EscapeDataString(sedeId)}");
        return parametros.Count == 0 ? ruta : $"{ruta}?{string.Join("&", parametros)}";
    }
}

public sealed record SolicitudProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    ColaboradorSolicitado Colaborador,
    IReadOnlyList<DateOnly> Fechas,
    SedeProgramada? Sede,
    Guid? JornadaId);

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
    bool Completo,
    VentanaDeEntradaFicha? VentanaDeEntrada = null,
    HorarioAbiertoFicha? HorarioAbierto = null);

public sealed record HorarioAbiertoFicha(string Duracion);

public sealed record VentanaDeEntradaFicha(TimeOnly Apertura, TimeOnly Cierre, string Amplitud);

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

public sealed record TurnoACrear(
    Guid TurnoId,
    string Nombre,
    bool EsDescanso,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<OrdinariaACrear>? Ordinarias);

public sealed record OrdinariaACrear(
    string Inicio,
    string Fin,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? DiaOffsetFin,
    IReadOnlyList<SubFranjaACrear>? Descansos,
    IReadOnlyList<SubFranjaACrear>? Extras,
    SedeProgramada? Sede);

public sealed record SubFranjaACrear(string Inicio, string Fin);
