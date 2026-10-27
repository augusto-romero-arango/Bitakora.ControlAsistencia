using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// Extraido de SolicitarProgramacionTurnoTool (MEF-ADR-0018: segunda aparicion, con
// CrearTurno/AgregarFranja/etc. ya planeadas en #609-#611). Encapsula GET programacion/turnos +
// normalizacion + busqueda por nombre. Solo lo consume crear_plantilla_semanal para clasificar sus
// turnos inline: ahi el nombre es la clave de unicidad (CA-ADR-0039 decision 18, punto 3).
public sealed partial class ResolutorTurnoPorNombre(ProgramacionApi programacion)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    // Resuelve N nombres con UNA sola lectura del catalogo (crear_plantilla_semanal, MEF-ADR-0047
    // decision 4: nunca un GET por nombre). Cada nombre solicitado conserva su posicion y su texto
    // original -- puede repetirse o venir con distinta capitalizacion.
    public async Task<ResultadoResolucionVariosTurnos> ResolverVariosAsync(
        IEnumerable<string> nombres, CancellationToken ct)
    {
        var respuesta = await programacion.ListarTurnos(ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionVariosTurnos([], fallo);

        var catalogo = (await respuesta.Content.ReadFromJsonAsync<SobreDeLista<FichaTurno>>(OpcionesLectura, ct))?.Elementos ?? [];
        var porNombreNormalizado = catalogo
            .GroupBy(f => NormalizarNombre(f.Nombre))
            .ToDictionary(g => g.Key, g => g.First());

        var resoluciones = nombres
            .Select(nombre => new ResolucionTurnoPorNombre(
                nombre,
                porNombreNormalizado.GetValueOrDefault(NormalizarNombre(nombre))))
            .ToList();

        return new ResultadoResolucionVariosTurnos(resoluciones, null);
    }

    // Duplicado deliberado de CrearTurnoCommandHandler.NormalizarNombre (MEF-ADR-0018): este
    // resolutor cruza de Mcp.Asistente hacia Programacion, sin ensamblado compartido entre ambos.
    private static string NormalizarNombre(string nombre) =>
        EspaciosConsecutivos().Replace(nombre.Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosConsecutivos();
}

/// <summary>Resolucion de un nombre solicitado dentro de un lote (ResolverVariosAsync); Ficha null = no encontrado.</summary>
public sealed record ResolucionTurnoPorNombre(string NombreSolicitado, FichaTurno? Ficha);

/// <summary>
/// Resultado de resolver varios turnos por nombre con una sola lectura del catalogo. Resoluciones
/// conserva el orden y las repeticiones de los nombres solicitados.
/// </summary>
public sealed record ResultadoResolucionVariosTurnos(
    IReadOnlyList<ResolucionTurnoPorNombre> Resoluciones,
    string? FalloDeLectura);
