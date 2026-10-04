using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

public sealed record ResultadoCandidatos(IReadOnlyList<CandidatoProgramacion> Candidatos, string? FalloDeLectura);

// Una sola llamada al directorio sin Take: las llamadas internas de composicion no paginan (CA-ADR-0038).
public sealed class ResolutorCandidatosPorLista(ColaboradoresApi colaboradores)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoCandidatos> ResolverAsync(IReadOnlyList<string> identificaciones, CancellationToken ct)
    {
        var respuesta = await colaboradores.BuscarEnDirectorio(identificaciones, null, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoCandidatos([], fallo);

        var directorio = await respuesta.Content.ReadFromJsonAsync<List<EntradaDirectorio>>(OpcionesLectura, ct) ?? [];
        var normalizadas = identificaciones.Select(i => i.Trim().ToUpperInvariant()).ToHashSet();

        var candidatos = directorio
            .Where(e => normalizadas.Contains(e.Identificacion.Trim().ToUpperInvariant()))
            .Select(e => new CandidatoProgramacion(
                e.Identificacion, e.CodigoColaborador, e.NombreCompleto, e.VigenteDesde, e.VigenteHasta))
            .ToList();

        return new ResultadoCandidatos(candidatos, null);
    }
}
