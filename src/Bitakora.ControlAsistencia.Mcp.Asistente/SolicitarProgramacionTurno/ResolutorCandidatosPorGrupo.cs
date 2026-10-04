using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

// Una sola llamada a fichas sin Take ni cursor: las llamadas internas de composicion no paginan (CA-ADR-0038).
public sealed class ResolutorCandidatosPorGrupo(ColaboradoresApi colaboradores)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoCandidatos> ResolverAsync(
        DateOnly fechaReferencia, string? codigoSede, IReadOnlyList<FiltroEtiqueta> etiquetas, CancellationToken ct)
    {
        var respuesta = await colaboradores.ListarFichas(fechaReferencia, codigoSede, etiquetas, null, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoCandidatos([], fallo);

        var fichas = await respuesta.Content.ReadFromJsonAsync<List<FichaColaborador>>(OpcionesLectura, ct) ?? [];
        var candidatos = fichas
            .Select(f => new CandidatoProgramacion(
                f.Id, f.CodigoColaborador, f.NombreCompleto, f.VigenteDesde, f.VigenteHasta, f.CodigoSede))
            .ToList();

        return new ResultadoCandidatos(candidatos, null);
    }
}
