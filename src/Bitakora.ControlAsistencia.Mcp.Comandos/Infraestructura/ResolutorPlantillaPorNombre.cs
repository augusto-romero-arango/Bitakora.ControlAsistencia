using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Calco de ResolutorTurnoPorNombre (MEF-ADR-0018) sobre ListarPlantillasSemanales: mismo criterio
// de normalizacion (trim + colapso de espacios + case-insensitive, acentos significativos) y mismo
// contrato de NombresDisponibles para el mensaje PlantillaNoExiste de la tool consumidora
// (MEF-ADR-0009). #628 reutiliza este resolutor.
public sealed partial class ResolutorPlantillaPorNombre(ProgramacionApi programacion)
{
    public const int MaximoPlantillasEnMensaje = 20;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    // El boundary del sistema (5xx o cuerpo no JSON) se traduce como fallo de lectura crudo: la
    // tool consumidora decide como formatearlo con su propia .resx RechazoDelDominio (CA-ADR-0030).
    public async Task<ResultadoResolucionPlantilla> ResolverAsync(string nombre, CancellationToken ct)
    {
        var respuesta = await programacion.ListarPlantillasSemanales(ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return new ResultadoResolucionPlantilla(null, fallo, []);

        var catalogo = await respuesta.Content.ReadFromJsonAsync<List<CuadroSemanalResumen>>(OpcionesLectura, ct) ?? [];
        var normalizado = NormalizarNombre(nombre);
        var ficha = catalogo.FirstOrDefault(f => NormalizarNombre(f.Nombre) == normalizado);

        return ficha is not null
            ? new ResultadoResolucionPlantilla(ficha, null, [])
            : new ResultadoResolucionPlantilla(
                null, null, [.. catalogo.Select(f => f.Nombre).Take(MaximoPlantillasEnMensaje)]);
    }

    private static string NormalizarNombre(string nombre) =>
        EspaciosConsecutivos().Replace(nombre.Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosConsecutivos();
}

/// <summary>
/// Resultado de resolver una plantilla semanal por nombre: exactamente uno de Ficha/FalloDeLectura
/// viene poblado cuando la plantilla se encuentra o la lectura del catalogo falla;
/// NombresDisponibles alimenta el mensaje PlantillaNoExiste de la tool consumidora cuando ninguno
/// de los dos aplica.
/// </summary>
public sealed record ResultadoResolucionPlantilla(
    CuadroSemanalResumen? Ficha,
    string? FalloDeLectura,
    IReadOnlyList<string> NombresDisponibles);
