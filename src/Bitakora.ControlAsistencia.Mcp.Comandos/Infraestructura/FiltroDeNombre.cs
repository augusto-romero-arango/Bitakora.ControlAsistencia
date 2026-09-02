using System.Globalization;
using System.Text;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Comparacion de texto sin distinguir mayusculas ni acentos, para los filtros de una tool de
/// consulta (MEF-ADR-0047 decision 4).
/// </summary>
public static class FiltroDeNombre
{
    public static bool Contiene(string texto, string filtro) =>
        Normalizar(texto).Contains(Normalizar(filtro), StringComparison.OrdinalIgnoreCase);

    private static string Normalizar(string valor)
    {
        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        var sinDiacriticos = new StringBuilder();

        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sinDiacriticos.Append(c);
        }

        return sinDiacriticos.ToString().Normalize(NormalizationForm.FormC);
    }
}
