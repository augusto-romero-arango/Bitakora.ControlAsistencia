using System.Globalization;
using System.Text;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// El PUT AsignarTurnoADia espera en la ruta el numero ISO 8601 del dia (1 = lunes).
public static class DiaSemanaMcp
{
    private static readonly IReadOnlyDictionary<string, int> NombresIso = new Dictionary<string, int>
    {
        ["LUNES"] = 1,
        ["MARTES"] = 2,
        ["MIERCOLES"] = 3,
        ["JUEVES"] = 4,
        ["VIERNES"] = 5,
        ["SABADO"] = 6,
        ["DOMINGO"] = 7
    };

    /// <summary>
    /// numeroIso en 1..7 (1 = lunes) si valor es un nombre de dia (con o sin acento, cualquier
    /// capitalizacion) o un numero "1".."7"; false para cualquier otro texto, incluido "0" y "8".
    /// </summary>
    public static bool TryParsear(string valor, out int numeroIso)
    {
        var normalizado = QuitarAcentos(valor.Trim()).ToUpperInvariant();

        if (NombresIso.TryGetValue(normalizado, out numeroIso))
            return true;

        if (int.TryParse(normalizado, out var numero) && numero is >= 1 and <= 7)
        {
            numeroIso = numero;
            return true;
        }

        numeroIso = 0;
        return false;
    }

    /// <summary>Nombre en espanol minusculas sin acento (ej. "miercoles") del numero ISO 1..7.</summary>
    public static string NombreDe(int numeroIso) =>
        NombresIso.Single(kv => kv.Value == numeroIso).Key.ToLowerInvariant();

    private static string QuitarAcentos(string valor) =>
        string.Concat(valor
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC);
}
