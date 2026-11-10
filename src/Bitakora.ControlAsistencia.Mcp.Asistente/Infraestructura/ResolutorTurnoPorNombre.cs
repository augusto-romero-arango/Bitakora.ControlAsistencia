using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// Solo para turnos inline: ahi el nombre es la clave de unicidad (CA-ADR-0039 decision 18, punto 3).
public sealed partial class ResolutorTurnoPorNombre(ProgramacionApi programacion)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    // Una sola lectura del catalogo para N nombres, nunca un GET por nombre (MEF-ADR-0047 decision 4).
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

    // Duplicado deliberado de CrearTurnoCommandHandler.NormalizarNombre (no hay ensamblado compartido
    // con Programacion): si cambia alli, cambia aqui.
    private static string NormalizarNombre(string nombre) =>
        EspaciosConsecutivos().Replace(nombre.Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosConsecutivos();
}

/// <summary>Resolucion de un nombre solicitado dentro de un lote (ResolverVariosAsync); Ficha null = no encontrado.</summary>
public sealed record ResolucionTurnoPorNombre(string NombreSolicitado, FichaTurno? Ficha);

/// <summary>Resoluciones conserva el orden, las repeticiones y el texto original de los nombres solicitados.</summary>
public sealed record ResultadoResolucionVariosTurnos(
    IReadOnlyList<ResolucionTurnoPorNombre> Resoluciones,
    string? FalloDeLectura);
