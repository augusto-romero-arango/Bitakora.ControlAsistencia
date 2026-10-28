using System.Text.RegularExpressions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Regla unica de unicidad de nombres del catalogo (turnos y plantillas de turnos): trim de
// extremos + colapso de espacios internos + case-folding. Los acentos SON significativos (decision
// del experto): ToUpperInvariant no los remueve, y por eso la comparacion es Ordinal.
internal static partial class NombreDeCatalogo
{
    internal static string Normalizar(string nombre) =>
        EspaciosConsecutivos().Replace(nombre.Trim(), " ").ToUpperInvariant();

    internal static bool EstaRepetido(string nombre, IEnumerable<string> nombresVigentes)
    {
        var normalizado = Normalizar(nombre);
        return nombresVigentes.Any(vigente =>
            string.Equals(Normalizar(vigente), normalizado, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosConsecutivos();
}
