using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ResolutorPlantillaTurnosPorId(ProgramacionApi programacion)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoResolucionPlantillaTurnosPorId> ResolverAsync(Guid plantillaId, CancellationToken ct)
    {
        var respuesta = await programacion.ObtenerPlantillaTurnos(plantillaId.ToString(), ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return new ResultadoResolucionPlantillaTurnosPorId(null, null, true);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionPlantillaTurnosPorId(null, fallo, false);

        var ficha = (await respuesta.Content.ReadFromJsonAsync<CuadroDeTurnos>(OpcionesLectura, ct))!;
        return new ResultadoResolucionPlantillaTurnosPorId(ficha, null, false);
    }
}

/// <summary>Exactamente uno de Ficha/FalloDeLectura/NoExiste viene poblado.</summary>
public sealed record ResultadoResolucionPlantillaTurnosPorId(CuadroDeTurnos? Ficha, string? FalloDeLectura, bool NoExiste);
