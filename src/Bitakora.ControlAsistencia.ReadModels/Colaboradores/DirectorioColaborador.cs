using System.Globalization;
using System.Text;

namespace Bitakora.ControlAsistencia.ReadModels.Colaboradores;

/// <summary>
/// Read model del directorio de colaboradores -- una entrada por colaborador, para ENCONTRAR a una
/// persona por nombre o numero de documento. Necesidad de lectura distinta de la ficha, que muestra
/// el detalle de una persona ya identificada (MEF-ADR-0041).
/// </summary>
/// <remarks>
/// Record plano SIN partial (MEF-ADR-0035): el comportamiento de proyeccion vive en la clase
/// companion DirectorioColaboradorProjection, en el worker. ReadModels es la cuarta isla del repo
/// (CA-ADR-0029): cero referencias de proyecto.
///
/// Id es el stream key de ColaboradorAggregateRoot ("{Tipo}-{Numero}", ej. "CC-79879078") y ES la
/// identificacion completa -- por eso NO existe un campo Identificacion aparte, seria redundante.
/// TipoDocumento/NumeroDocumento son la forma descompuesta de esa misma identidad, necesaria para
/// buscar por numero suelto ("79879078", sin el prefijo de tipo).
///
/// CodigoColaborador, VigenteDesde, VigenteHasta y CodigoSede reflejan siempre la ULTIMA
/// vinculacion: un reingreso los sobrescribe, y un reingreso sin sede LIMPIA la anterior en vez de
/// heredarla ("reingreso nace limpio"). Sin ShouldDelete: el directorio nunca borra -- un
/// colaborador terminado sigue apareciendo con su VigenteHasta y el consumidor decide.
///
/// No lleva etiquetas: la busqueda por grupo sigue siendo del QUERY de fichas.
/// </remarks>
public sealed record DirectorioColaborador(
    string Id,
    string TipoDocumento,
    string NumeroDocumento,
    string NombreCompleto,
    IReadOnlyList<string> TokensNombre,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly VigenteHasta,
    string? CodigoSede = null)
{
    private const char SeparadorDeTokens = ' ';

    /// <summary>
    /// Valor de <see cref="VigenteHasta"/> que significa "vinculacion abierta" (sin terminacion
    /// registrada).
    /// </summary>
    /// <remarks>
    /// Constante PROPIA de esta vista aunque coincida con FichaColaborador.CentinelaVigenciaAbierta:
    /// cada vista codifica sus propios campos. Vive con la vista y no con la proyeccion porque la
    /// comparten dos procesos que solo se ven a traves de ReadModels -- el worker que la escribe y
    /// el endpoint que la lee.
    /// </remarks>
    public static readonly DateOnly CentinelaVigenciaAbierta = new(9999, 12, 31);

    /// <summary>
    /// Tokeniza un nombre para busqueda: minusculas, sin diacriticos, un token por palabra separando
    /// por todo caracter que no sea letra ni digito, sin duplicados, en orden de aparicion.
    /// </summary>
    /// <remarks>
    /// La expone la vista (Tell-don't-Ask, MEF-ADR-0012) porque la usan dos procesos que no se
    /// referencian entre si -- la proyeccion al escribir TokensNombre y el endpoint de consulta al
    /// tokenizar el termino de busqueda del cliente: un algoritmo por lado divergiria en silencio.
    /// </remarks>
    public static IReadOnlyList<string> TokenizarNombre(string nombre)
    {
        // Quitar diacriticos duplica a proposito Etiqueta.Normalizar (Colaboradores.DomainEvents):
        // cruza islas, ReadModels no referencia DomainEvents (MEF-ADR-0018). Colapsa la enie --
        // "Munoz" y "Munoz" con virgulilla son el mismo token, consecuencia deseada para buscar.
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
    /// Misma regla que Identificacion.Crear (Colaboradores.DomainEvents), duplicada a proposito:
    /// cruza islas, ReadModels no referencia DomainEvents (MEF-ADR-0018). Expuesta por la vista por
    /// la misma razon que <see cref="TokenizarNombre"/>.
    /// </remarks>
    public static string NormalizarNumeroDocumento(string numero) =>
        new(numero.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
