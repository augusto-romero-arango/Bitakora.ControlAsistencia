using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Colaboradores. Los listados son QUERY (RFC 10008,
/// MEF-ADR-0042): verbo no estandar con body JSON. Devuelve el HttpResponseMessage crudo: el
/// manejo de status y el remodelado pertenecen a cada tool (MEF-ADR-0047 decision 3).
/// </summary>
public sealed class ColaboradoresApi(HttpClient http)
{
    private static readonly HttpMethod Query = new("QUERY");

    // Body compacto (MEF-ADR-0047 decision 4, aplicada al request): un criterio ausente se omite
    // en vez de viajar como null.
    private static readonly JsonSerializerOptions OpcionesSinNulls = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<HttpResponseMessage> ListarFichas(
        DateOnly fechaReferencia,
        string? codigoSede,
        IReadOnlyList<FiltroEtiqueta> etiquetas,
        int? take,
        CancellationToken ct,
        CursorFichas? cursor = null)
    {
        var request = new HttpRequestMessage(Query, "api/colaboradores/fichas")
        {
            Content = JsonContent.Create(new
            {
                fechaReferencia,
                codigoSede,
                etiquetas = etiquetas.Count > 0 ? etiquetas : null,
                take,
                cursor
            }, options: OpcionesSinNulls)
        };

        return http.SendAsync(request, ct);
    }

    public Task<HttpResponseMessage> ObtenerFicha(string identificacion, CancellationToken ct) =>
        http.GetAsync($"api/colaboradores/fichas/{Uri.EscapeDataString(identificacion)}", ct);

    public Task<HttpResponseMessage> BuscarEnDirectorio(
        IReadOnlyList<string> identificaciones, int? take, CancellationToken ct) =>
        BuscarEnDirectorio(identificaciones, null, take, ct);

    public Task<HttpResponseMessage> BuscarEnDirectorio(
        IReadOnlyList<string>? identificaciones, string? nombre, int? take, CancellationToken ct)
    {
        var request = new HttpRequestMessage(Query, "api/colaboradores/directorio")
        {
            Content = JsonContent.Create(
                new
                {
                    // Lista vacia -> null: el endpoint responde 422 a "identificaciones": [] (#590).
                    identificaciones = identificaciones is { Count: > 0 } ? identificaciones : null,
                    nombre,
                    take
                },
                options: OpcionesSinNulls)
        };

        return http.SendAsync(request, ct);
    }

    public Task<HttpResponseMessage> Registrar(RegistroColaboradorSolicitado datos, CancellationToken ct) =>
        http.PostAsJsonAsync("api/colaboradores", datos, ct);
}

/// <summary>Cursor keyset {NombreCompleto, Id} de la ultima ficha de la pagina anterior.</summary>
public sealed record CursorFichas(string NombreCompleto, string Id);

/// <summary>Par categoria:valor SIN normalizar, tal como lo espera el body del QUERY upstream.</summary>
public sealed record FiltroEtiqueta(string Categoria, string Valor);

/// <summary>
/// Payload propio de la tool hacia POST /api/colaboradores (MEF-ADR-0039 decision 6). Serializa a
/// camelCase; FechaInicio viaja como DateOnly (yyyy-MM-dd).
/// </summary>
public sealed record RegistroColaboradorSolicitado(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido,
    string CodigoColaborador,
    DateOnly FechaInicio,
    string? CodigoSede);
