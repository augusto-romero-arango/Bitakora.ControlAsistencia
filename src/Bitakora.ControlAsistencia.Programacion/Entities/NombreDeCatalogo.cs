using System.Text.RegularExpressions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Regla unica de unicidad de nombres del catalogo (turnos y plantillas semanales): trim de
// extremos + colapso de espacios internos + case-folding. Los acentos SON significativos
// (decision del experto, issue #497): ToUpperInvariant no los remueve, y por eso la comparacion
// final debe ser Ordinal sobre nombres ya normalizados.
internal static partial class NombreDeCatalogo
{
    internal static string Normalizar(string nombre) =>
        EspaciosConsecutivos().Replace(nombre.Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosConsecutivos();
}
