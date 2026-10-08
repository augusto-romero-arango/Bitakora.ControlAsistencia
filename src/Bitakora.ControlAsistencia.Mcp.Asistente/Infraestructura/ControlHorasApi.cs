using System.Net.Http.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de ControlHoras. El endpoint de turnos vigentes es un QUERY
/// (RFC 10008, MEF-ADR-0042): verbo no estandar con body JSON, que HttpClient soporta via
/// HttpMethod arbitrario.
/// </summary>
public sealed class ControlHorasApi(HttpClient http)
{
    private static readonly HttpMethod Query = new("QUERY");

    public Task<HttpResponseMessage> ConsultarTurnosVigentes(
        DateOnly desde,
        DateOnly hasta,
        string? codigoColaborador,
        string? sedeId,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(Query, "api/control-horas/turnos-vigentes")
        {
            Content = JsonContent.Create(new
            {
                desdeFecha = desde,
                hastaFecha = hasta,
                codigoColaborador,
                sedeId
            })
        };

        return http.SendAsync(request, ct);
    }

    public Task<HttpResponseMessage> ListarAdvertenciasProgramacionSemanal(
        DateOnly fecha,
        IReadOnlyList<string>? codigosColaborador,
        int take,
        string? cursor,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(Query, "api/control-horas/advertencias-programacion-semanal")
        {
            Content = JsonContent.Create(new
            {
                fecha,
                codigosColaborador,
                soloConAdvertencias = true,
                take,
                cursor
            })
        };

        return http.SendAsync(request, ct);
    }
}
