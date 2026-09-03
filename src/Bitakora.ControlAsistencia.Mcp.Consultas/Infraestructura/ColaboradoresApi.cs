using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Colaboradores. El listado es un QUERY (RFC 10008,
/// MEF-ADR-0042): verbo no estandar con body JSON, igual que <see cref="ControlHorasApi"/>. Ver
/// <see cref="ProgramacionApi"/> para el criterio de devolver el HttpResponseMessage crudo.
/// </summary>
public sealed class ColaboradoresApi(HttpClient http)
{
    private static readonly HttpMethod Query = new("QUERY");

    // Criterios ausentes (nombre/identificaciones) no deben viajar como null en el body: el
    // endpoint upstream distingue "campo ausente" de "campo null" (#590).
    private static readonly JsonSerializerOptions OpcionesSinNulls = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<HttpResponseMessage> ListarFichas(
        DateOnly fechaReferencia,
        string? codigoSede,
        IReadOnlyList<FiltroEtiqueta> etiquetas,
        int take,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(Query, "api/colaboradores/fichas")
        {
            Content = JsonContent.Create(new
            {
                fechaReferencia,
                codigoSede,
                etiquetas = etiquetas.Count > 0 ? etiquetas : null,
                take
            })
        };

        return http.SendAsync(request, ct);
    }

    public Task<HttpResponseMessage> ObtenerFicha(string identificacion, CancellationToken ct) =>
        http.GetAsync($"api/colaboradores/fichas/{Uri.EscapeDataString(identificacion)}", ct);

    public Task<HttpResponseMessage> BuscarEnDirectorio(
        IReadOnlyList<string>? identificaciones, string? nombre, int take, CancellationToken ct)
    {
        var request = new HttpRequestMessage(Query, "api/colaboradores/directorio")
        {
            Content = JsonContent.Create(
                new
                {
                    identificaciones = identificaciones is { Count: > 0 } ? identificaciones : null,
                    nombre,
                    take
                },
                options: OpcionesSinNulls)
        };

        return http.SendAsync(request, ct);
    }
}

/// <summary>
/// Par categoria:valor SIN normalizar, tal como lo espera el body del QUERY upstream
/// (FiltroListarFichasColaborador.Etiquetas en Bitakora.ControlAsistencia.Colaboradores).
/// </summary>
public sealed record FiltroEtiqueta(string Categoria, string Valor);
