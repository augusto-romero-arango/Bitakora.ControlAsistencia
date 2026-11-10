using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ResolutorSedePorCodigo(SedesApi sedes)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoResolucionSedeId> ResolverIdAsync(string codigo, CancellationToken ct)
    {
        var respuesta = await sedes.ObtenerFicha(codigo, ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return new ResultadoResolucionSedeId(null, null, true);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionSedeId(null, fallo, false);

        var ficha = (await respuesta.Content.ReadFromJsonAsync<FichaSede>(OpcionesLectura, ct))!;
        return new ResultadoResolucionSedeId(ficha.Codigo, null, false);
    }

    // 5xx o cuerpo no JSON vuelven como fallo de lectura crudo: la tool consumidora lo formatea con su
    // RechazoDelDominio.
    public async Task<ResultadoResolucionSede> ResolverAsync(string codigo, CancellationToken ct)
    {
        var respuesta = await sedes.ObtenerFicha(codigo, ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return new ResultadoResolucionSede(null, null, MotivoSedeNoResuelta.NoExiste);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionSede(null, fallo, null);

        var ficha = (await respuesta.Content.ReadFromJsonAsync<FichaSede>(OpcionesLectura, ct))!;

        return ficha.Activa
            ? new ResultadoResolucionSede(
                new SedeProgramada(ficha.Codigo, ficha.Nombre, ficha.CentroDeCostos), null, null)
            : new ResultadoResolucionSede(null, null, MotivoSedeNoResuelta.Inactiva);
    }
}

/// <summary>
/// Resuelve sin exigir que la sede este activa. SedeId es el codigo canonico del maestro (el mismo
/// SedeProgramada.Id que graba ResolverAsync), nunca el stream key "s:{codigo}" (CA-ADR-0031).
/// </summary>
public sealed record ResultadoResolucionSedeId(string? SedeId, string? FalloDeLectura, bool NoExiste);

/// <summary>Exactamente uno de Sede/FalloDeLectura/Motivo viene poblado.</summary>
public sealed record ResultadoResolucionSede(
    SedeProgramada? Sede,
    string? FalloDeLectura,
    MotivoSedeNoResuelta? Motivo)
{
    /// <summary>
    /// El mensaje que la tool debe devolver por el motivo del rechazo, o null si la sede se
    /// resolvio. Recibe las plantillas del .resx de la tool (MEF-ADR-0009) en vez de tenerlas:
    /// asi el mapeo motivo -> mensaje es exhaustivo en un solo lugar, y un motivo nuevo rompe la
    /// compilacion de cada consumidora en vez de caer en el mensaje equivocado.
    /// </summary>
    public string? MensajeDelMotivo(string codigo, string noExiste, string inactiva) =>
        Motivo switch
        {
            MotivoSedeNoResuelta.NoExiste => string.Format(noExiste, codigo),
            MotivoSedeNoResuelta.Inactiva => string.Format(inactiva, codigo),
            _ => null
        };
}

/// <summary>Por que un codigo de sede no produjo una sede prearmable.</summary>
public enum MotivoSedeNoResuelta
{
    NoExiste,
    Inactiva
}
