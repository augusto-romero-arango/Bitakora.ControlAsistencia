using System.Globalization;
using System.Text;

namespace Bitakora.ControlAsistencia.ReadModels.Colaboradores;

/// <summary>
/// Directorio de colaboradores, una entrada por colaborador, para ENCONTRAR a una persona por nombre
/// o numero de documento; el detalle de una persona ya identificada es la ficha.
/// </summary>
/// <remarks>
/// Id es el stream key ("{Tipo}-{Numero}", ej. "CC-79879078") y ES la identificacion completa.
/// TipoDocumento/NumeroDocumento la descomponen para buscar por numero suelto.
///
/// CodigoColaborador, VigenteDesde, VigenteHasta y CodigoSede reflejan la ULTIMA vinculacion; un
/// reingreso sin sede LIMPIA la anterior ("reingreso nace limpio"). El directorio nunca borra: un
/// colaborador terminado sigue apareciendo con su VigenteHasta y el consumidor decide.
///
/// Sin etiquetas: la busqueda por grupo es del QUERY de fichas.
/// </remarks>
[MaterializadoPorProyeccion]
public sealed record DirectorioColaborador(
    string Id,
    string TipoDocumento,
    string NumeroDocumento,
    string NombreCompleto,
    IReadOnlyList<string> TokensNombre,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly VigenteHasta,
    string? CodigoSede = null,
    Guid? JornadaId = null)
{
    private const char SeparadorDeTokens = ' ';

    /// <summary>
    /// Valor de <see cref="VigenteHasta"/> que significa "vinculacion abierta" (sin terminacion
    /// registrada).
    /// </summary>
    /// <remarks>
    /// Propia de esta vista aunque coincida con FichaColaborador.CentinelaVigenciaAbierta: cada vista
    /// codifica sus propios campos.
    /// </remarks>
    public static readonly DateOnly CentinelaVigenciaAbierta = new(9999, 12, 31);

    /// <summary>
    /// Tokeniza un nombre para busqueda: minusculas, sin diacriticos, un token por palabra separando
    /// por todo caracter que no sea letra ni digito, sin duplicados, en orden de aparicion.
    /// </summary>
    /// <remarks>
    /// La usan la proyeccion al escribir TokensNombre y el endpoint al tokenizar el termino de
    /// busqueda: un algoritmo por lado divergiria en silencio.
    /// </remarks>
    public static IReadOnlyList<string> TokenizarNombre(string nombre)
    {
        // Duplica a proposito Etiqueta.Normalizar: ReadModels no referencia DomainEvents. Colapsar
        // la enie es deseado para buscar.
        var sinDiacriticos = new string(nombre.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        var separadoPorEspacios = new string(sinDiacriticos
            .Select(c => char.IsLetterOrDigit(c) ? c : SeparadorDeTokens)
            .ToArray());

        // Where preserva el orden de aparicion; Distinct no lo garantiza por contrato.
        var yaVistos = new HashSet<string>();
        return separadoPorEspacios
            .Split(SeparadorDeTokens, StringSplitOptions.RemoveEmptyEntries)
            .Where(yaVistos.Add)
            .ToList();
    }

    /// <summary>
    /// Normaliza un numero de documento: conserva solo caracteres ASCII letra/digito, en mayusculas
    /// invariantes ("79.879.078" y " ab-123.456 " se vuelven "79879078" y "AB123456").
    /// </summary>
    /// <remarks>
    /// Misma regla que Identificacion.Crear, duplicada a proposito: ReadModels no referencia
    /// DomainEvents.
    /// </remarks>
    public static string NormalizarNumeroDocumento(string numero) =>
        new(numero.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
