using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// Lee el detalle de un turno por identidad (GET programacion/turnos/{id}) en vez del catalogo
// completo (CA-ADR-0039 decision 18). Cada tool consumidora aporta sus mensajes desde su .resx
// (MEF-ADR-0009); el fallo de lectura se devuelve crudo para su RechazoDelDominio (CA-ADR-0030).
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
