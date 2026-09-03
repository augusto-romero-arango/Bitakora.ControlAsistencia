using System.Net.Http.Json;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Cliente tipado del Function App de Colaboradores. Ver el comentario de ConfiguracionClientesHttp
/// para el criterio de devolver el HttpResponseMessage crudo (MEF-ADR-0047 decision 3): el manejo
/// de status y el remodelado pertenecen a cada tool, no a este cliente.
/// </summary>
public sealed class ColaboradoresApi(HttpClient http)
{
    private static readonly HttpMethod Query = new("QUERY");

    public Task<HttpResponseMessage> Registrar(RegistroColaboradorSolicitado datos, CancellationToken ct) =>
        http.PostAsJsonAsync("api/colaboradores", datos, ct);

    // QUERY (RFC 10008, MEF-ADR-0042): verbo no estandar con body JSON, mismo precedente que
    // Mcp.Consultas.Infraestructura.ColaboradoresApi.BuscarEnDirectorio.
    public Task<HttpResponseMessage> BuscarEnDirectorio(
        IReadOnlyList<string> identificaciones, int take, CancellationToken ct)
    {
        var request = new HttpRequestMessage(Query, "api/colaboradores/directorio")
        {
            Content = JsonContent.Create(new { identificaciones, take })
        };

        return http.SendAsync(request, ct);
    }
}

/// <summary>
/// Entrada del directorio tal como la devuelve QUERY colaboradores/directorio -- solo los campos
/// que esta tool consume (MEF-ADR-0047 decision 3: contrato propio, no el read model del dominio).
/// </summary>
public sealed record EntradaDirectorio(
    string Identificacion,
    string NombreCompleto,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta);

/// <summary>
/// Payload propio de la tool hacia POST /api/colaboradores (MEF-ADR-0039 decision 6: el comando
/// nunca reusa un tipo de un ensamblado de eventos). Serializa a camelCase, contrato exacto del
/// comando RegistrarColaborador (MEF-ADR-0043); FechaInicio viaja como DateOnly (yyyy-MM-dd).
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
