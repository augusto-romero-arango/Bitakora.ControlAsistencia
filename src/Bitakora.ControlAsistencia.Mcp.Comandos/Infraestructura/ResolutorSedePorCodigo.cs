using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Extraido de solicitar_programacion_turno, agregar_franja y asignar_sede_franja (MEF-ADR-0018:
// tercera aparicion del mismo remodelado, propuesta en el issue #611). Encapsula
// GET sedes/fichas/{codigo} + 404 + sede inactiva + traduccion a SedeProgramada, igual que
// ResolutorTurnoPorNombre hace con el catalogo de turnos; cada tool consumidora sigue aportando
// sus propios mensajes desde su .resx (MEF-ADR-0009).
public sealed class ResolutorSedePorCodigo(SedesApi sedes)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    // El boundary del sistema (5xx o cuerpo no JSON) se traduce como fallo de lectura crudo: la
    // tool consumidora decide como formatearlo con su propia .resx RechazoDelDominio (CA-ADR-0030).
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
/// Resultado de resolver una sede por codigo: exactamente uno de Sede/FalloDeLectura/Motivo viene
/// poblado, mismo contrato que ResultadoResolucionTurno.
/// </summary>
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
