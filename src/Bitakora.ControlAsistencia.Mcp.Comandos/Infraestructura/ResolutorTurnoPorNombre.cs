using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Extraido de SolicitarProgramacionTurnoTool (MEF-ADR-0018: segunda aparicion, con
// CrearTurno/AgregarFranja/etc. ya planeadas en #609-#611). Encapsula GET programacion/turnos +
// normalizacion + busqueda por nombre; cada tool consumidora sigue formateando su propio mensaje
// de "no encontrado" con su .resx (MEF-ADR-0009) a partir de NombresDisponibles.
public class ResolutorTurnoPorNombre(ProgramacionApi programacion)
{
    public const int MaximoTurnosEnMensaje = 20;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);
    private static readonly Regex EspaciosConsecutivos = new(@"\s+", RegexOptions.Compiled);

    // El boundary del sistema (5xx o cuerpo no JSON) se traduce como fallo de lectura crudo: la
    // tool consumidora decide como formatearlo con su propia .resx RechazoDelDominio (CA-ADR-0030).
    public async Task<ResultadoResolucionTurno> ResolverAsync(string nombre, CancellationToken ct)
    {
        var respuesta = await programacion.ListarTurnos(ct);
        if (!respuesta.IsSuccessStatusCode)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            var fallo = string.IsNullOrWhiteSpace(cuerpo) ? ((int)respuesta.StatusCode).ToString() : cuerpo;
            return new ResultadoResolucionTurno(null, fallo, []);
        }

        var catalogo = await respuesta.Content.ReadFromJsonAsync<List<FichaTurno>>(OpcionesLectura, ct) ?? [];
        var normalizado = NormalizarNombre(nombre);
        var ficha = catalogo.FirstOrDefault(f => NormalizarNombre(f.Nombre) == normalizado);

        return ficha is not null
            ? new ResultadoResolucionTurno(ficha, null, [])
            : new ResultadoResolucionTurno(
                null, null, [.. catalogo.Select(f => f.Nombre).Take(MaximoTurnosEnMensaje)]);
    }

    // Duplicado deliberado de CrearTurnoCommandHandler.NormalizarNombre (MEF-ADR-0018): este
    // resolutor cruza de Mcp.Comandos hacia Programacion, sin ensamblado compartido entre ambos.
    private static string NormalizarNombre(string nombre) =>
        EspaciosConsecutivos.Replace(nombre.Trim(), " ").ToUpperInvariant();
}

/// <summary>
/// Resultado de resolver un turno por nombre: exactamente uno de Ficha/FalloDeLectura viene
/// poblado cuando el turno se encuentra o la lectura del catalogo falla; NombresDisponibles
/// alimenta el mensaje TurnoNoExiste de la tool consumidora cuando ninguno de los dos aplica.
/// </summary>
public sealed record ResultadoResolucionTurno(
    FichaTurno? Ficha,
    string? FalloDeLectura,
    IReadOnlyList<string> NombresDisponibles);
