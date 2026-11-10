using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// Lee el turno por identidad y no el catalogo completo (CA-ADR-0039 decision 18); el fallo de lectura
// vuelve crudo para el RechazoDelDominio de cada tool.
public sealed class ResolutorTurnoPorId(ProgramacionApi programacion)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoResolucionTurnoPorId> ResolverAsync(Guid turnoId, CancellationToken ct)
    {
        var respuesta = await programacion.ObtenerTurno(turnoId.ToString(), ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return new ResultadoResolucionTurnoPorId(null, null, true);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionTurnoPorId(null, fallo, false);

        var ficha = (await respuesta.Content.ReadFromJsonAsync<FichaTurno>(OpcionesLectura, ct))!;
        return new ResultadoResolucionTurnoPorId(ficha, null, false);
    }
}

/// <summary>Exactamente uno de Ficha/FalloDeLectura/NoExiste viene poblado.</summary>
public sealed record ResultadoResolucionTurnoPorId(FichaTurno? Ficha, string? FalloDeLectura, bool NoExiste);
