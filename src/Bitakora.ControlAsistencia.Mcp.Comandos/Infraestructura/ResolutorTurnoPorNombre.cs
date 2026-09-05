using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Extraido de SolicitarProgramacionTurnoTool (MEF-ADR-0018: segunda aparicion, con
// CrearTurno/AgregarFranja/etc. ya planeadas en #609-#611). Encapsula GET programacion/turnos +
// normalizacion + busqueda por nombre; cada tool consumidora sigue formateando su propio mensaje
// de "no encontrado" con su .resx (MEF-ADR-0009) a partir de NombresDisponibles.
public sealed partial class ResolutorTurnoPorNombre(ProgramacionApi programacion)
{
    public const int MaximoTurnosEnMensaje = 20;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    // El boundary del sistema (5xx o cuerpo no JSON) se traduce como fallo de lectura crudo: la
    // tool consumidora decide como formatearlo con su propia .resx RechazoDelDominio (CA-ADR-0030).
    public async Task<ResultadoResolucionTurno> ResolverAsync(string nombre, CancellationToken ct)
    {
        var respuesta = await programacion.ListarTurnos(ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionTurno(null, fallo, []);

        var catalogo = await respuesta.Content.ReadFromJsonAsync<List<FichaTurno>>(OpcionesLectura, ct) ?? [];
        var normalizado = NormalizarNombre(nombre);
        var ficha = catalogo.FirstOrDefault(f => NormalizarNombre(f.Nombre) == normalizado);

        return ficha is not null
            ? new ResultadoResolucionTurno(ficha, null, [])
            : new ResultadoResolucionTurno(
                null, null, [.. catalogo.Select(f => f.Nombre).Take(MaximoTurnosEnMensaje)]);
    }

    // Resuelve N nombres con UNA sola lectura del catalogo (crear_plantilla_semanal, MEF-ADR-0047
    // decision 4: nunca un GET por nombre). Cada nombre solicitado conserva su posicion y su texto
    // original -- puede repetirse o venir con distinta capitalizacion -- para que la tool
    // consumidora arme el mensaje de "faltantes" con el texto que el usuario/modelo escribio.
    public async Task<ResultadoResolucionVariosTurnos> ResolverVariosAsync(
        IEnumerable<string> nombres, CancellationToken ct)
    {
        var respuesta = await programacion.ListarTurnos(ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionVariosTurnos([], fallo, []);

        var catalogo = await respuesta.Content.ReadFromJsonAsync<List<FichaTurno>>(OpcionesLectura, ct) ?? [];
        var porNombreNormalizado = catalogo
            .GroupBy(f => NormalizarNombre(f.Nombre))
            .ToDictionary(g => g.Key, g => g.First());

        var resoluciones = nombres
            .Select(nombre => new ResolucionTurnoPorNombre(
                nombre,
                porNombreNormalizado.GetValueOrDefault(NormalizarNombre(nombre))))
            .ToList();

        return new ResultadoResolucionVariosTurnos(
            resoluciones, null, [.. catalogo.Select(f => f.Nombre).Take(MaximoTurnosEnMensaje)]);
    }

    // Duplicado deliberado de CrearTurnoCommandHandler.NormalizarNombre (MEF-ADR-0018): este
    // resolutor cruza de Mcp.Comandos hacia Programacion, sin ensamblado compartido entre ambos.
    private static string NormalizarNombre(string nombre) =>
        EspaciosConsecutivos().Replace(nombre.Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosConsecutivos();
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

/// <summary>Resolucion de un nombre solicitado dentro de un lote (ResolverVariosAsync); Ficha null = no encontrado.</summary>
public sealed record ResolucionTurnoPorNombre(string NombreSolicitado, FichaTurno? Ficha);

/// <summary>
/// Resultado de resolver varios turnos por nombre con una sola lectura del catalogo. Resoluciones
/// conserva el orden y las repeticiones de los nombres solicitados; NombresDisponibles alimenta el
/// mensaje TurnosNoExisten de la tool consumidora (maximo MaximoTurnosEnMensaje).
/// </summary>
public sealed record ResultadoResolucionVariosTurnos(
    IReadOnlyList<ResolucionTurnoPorNombre> Resoluciones,
    string? FalloDeLectura,
    IReadOnlyList<string> NombresDisponibles);
