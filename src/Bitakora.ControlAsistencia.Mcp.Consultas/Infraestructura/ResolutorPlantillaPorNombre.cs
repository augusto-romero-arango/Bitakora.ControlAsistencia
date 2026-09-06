using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

// Calco de ResolutorPlantillaPorNombre de Mcp.Comandos (MEF-ADR-0018: criterio replicado como
// texto/codigo propio, islas) sobre ListarPlantillasSemanales: mismo criterio de normalizacion
// (trim + colapso de espacios + case-insensitive, acentos significativos) y mismo contrato de
// NombresDisponibles para el mensaje PlantillaNoExiste de la tool consumidora (MEF-ADR-0009).
//
// Contains en vez de igualdad estricta (deviacion del analogo de Comandos, documentada en el
// resumen del pipeline): el asistente puede omitir un prefijo de catalogacion al escribir el
// nombre; los acentos siguen siendo significativos porque Contains aqui es ordinal.
public sealed partial class ResolutorPlantillaPorNombre(ProgramacionApi programacion)
{
    public const int MaximoPlantillasEnMensaje = 20;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoResolucionPlantilla> ResolverAsync(string nombre, CancellationToken ct)
    {
        var respuesta = await programacion.ListarPlantillasSemanales(ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionPlantilla(null, fallo, []);

        var catalogo = await respuesta.Content.ReadFromJsonAsync<List<CuadroSemanalTurnos>>(OpcionesLectura, ct) ?? [];
        var normalizado = NormalizarNombre(nombre);
        var cuadro = catalogo.FirstOrDefault(c => NormalizarNombre(c.Nombre).Contains(normalizado));

        return cuadro is not null
            ? new ResultadoResolucionPlantilla(cuadro, null, [])
            : new ResultadoResolucionPlantilla(
                null, null, [.. catalogo.Select(c => c.Nombre).Take(MaximoPlantillasEnMensaje)]);
    }

    private static string NormalizarNombre(string nombre) =>
        EspaciosConsecutivos().Replace(nombre.Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosConsecutivos();
}

/// <summary>
/// Resultado de resolver una plantilla semanal por nombre: exactamente uno de
/// Cuadro/FalloDeLectura viene poblado cuando la plantilla se encuentra o la lectura del catalogo
/// falla; NombresDisponibles alimenta el mensaje PlantillaNoExiste de la tool consumidora cuando
/// ninguno de los dos aplica.
/// </summary>
public sealed record ResultadoResolucionPlantilla(
    CuadroSemanalTurnos? Cuadro,
    string? FalloDeLectura,
    IReadOnlyList<string> NombresDisponibles);
