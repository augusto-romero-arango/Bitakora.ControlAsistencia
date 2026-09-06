using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

// Calco de ResolutorPlantillaPorNombre de Mcp.Comandos (MEF-ADR-0018: criterio replicado como
// texto/codigo propio, islas) sobre ListarPlantillasSemanales: mismo criterio de normalizacion
// (trim + colapso de espacios + case-insensitive, acentos significativos) y mismo contrato de
// NombresDisponibles para el mensaje PlantillaNoExiste de la tool consumidora (MEF-ADR-0009).
public sealed partial class ResolutorPlantillaPorNombre(ProgramacionApi programacion)
{
    public const int MaximoPlantillasEnMensaje = 20;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    public Task<ResultadoResolucionPlantilla> ResolverAsync(string nombre, CancellationToken ct) =>
        throw new NotImplementedException();

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
