using System.Net.Http.Json;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

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
}
