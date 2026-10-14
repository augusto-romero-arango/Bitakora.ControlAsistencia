using System.Globalization;
using System.Text;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public enum TipoCoincidenciaDePalabra
{
    Exacta,
    Prefijo,
    UnaLetra
}

public sealed record PalabraCoincidente(string Pedida, string Encontrada, TipoCoincidenciaDePalabra Tipo);

public static class CoincidenciaDeTexto
{
    private const int LongitudMinimaParaTolerancia = 4;

    /// <summary>Palabras pedidas que coinciden con alguna palabra del texto; vacio si ninguna.</summary>
    public static IReadOnlyList<PalabraCoincidente> Evaluar(string pedido, string texto)
    {
        var palabrasDelTexto = Palabras(texto);

        return Palabras(pedido)
            .Select(pedida => MejorCoincidencia(pedida, palabrasDelTexto))
            .OfType<PalabraCoincidente>()
            .ToList();
    }

    private static PalabraCoincidente? MejorCoincidencia(string pedida, IReadOnlyList<string> palabras) =>
        palabras
            .Select(p => Clasificar(pedida, p))
            .OfType<PalabraCoincidente>()
            .OrderBy(c => c.Tipo)
            .FirstOrDefault();

    private static PalabraCoincidente? Clasificar(string pedida, string palabra)
    {
        if (pedida == palabra)
            return new PalabraCoincidente(pedida, palabra, TipoCoincidenciaDePalabra.Exacta);

        if (palabra.StartsWith(pedida, StringComparison.Ordinal))
            return new PalabraCoincidente(pedida, palabra, TipoCoincidenciaDePalabra.Prefijo);

        return pedida.Length >= LongitudMinimaParaTolerancia && AUnaLetra(pedida, palabra)
            ? new PalabraCoincidente(pedida, palabra, TipoCoincidenciaDePalabra.UnaLetra)
            : null;
    }

    private static bool AUnaLetra(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1)
            return false;

        var (corta, larga) = a.Length <= b.Length ? (a, b) : (b, a);
        var i = 0;
        while (i < corta.Length && corta[i] == larga[i])
            i++;

        if (i == corta.Length)
            return true;

        return corta.Length == larga.Length
            ? corta[(i + 1)..] == larga[(i + 1)..]
            : corta[i..] == larga[(i + 1)..];
    }

    internal static IReadOnlyList<string> Palabras(string valor) =>
        Normalizar(valor)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Normalizar(string valor)
    {
        var sinDiacriticos = valor.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');

        return new string(sinDiacriticos.ToArray());
    }
}
