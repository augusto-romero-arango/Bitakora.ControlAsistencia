using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// Lee el detalle de una plantilla por identidad (GET programacion/plantillas-semanales/{id}) en
// vez del catalogo completo (CA-ADR-0039 decision 18). El fallo de lectura se devuelve crudo para
// el RechazoDelDominio de la tool consumidora (CA-ADR-0030).
public sealed class ResolutorPlantillaPorId(ProgramacionApi programacion)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoResolucionPlantillaPorId> ResolverAsync(Guid plantillaId, CancellationToken ct)
    {
        var respuesta = await programacion.ObtenerPlantillaSemanal(plantillaId.ToString(), ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return new ResultadoResolucionPlantillaPorId(null, null, true);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionPlantillaPorId(null, fallo, false);

        var ficha = (await respuesta.Content.ReadFromJsonAsync<CuadroSemanalTurnos>(OpcionesLectura, ct))!;
        return new ResultadoResolucionPlantillaPorId(ficha, null, false);
    }
}

/// <summary>Exactamente uno de Ficha/FalloDeLectura/NoExiste viene poblado.</summary>
public sealed record ResultadoResolucionPlantillaPorId(CuadroSemanalTurnos? Ficha, string? FalloDeLectura, bool NoExiste);
