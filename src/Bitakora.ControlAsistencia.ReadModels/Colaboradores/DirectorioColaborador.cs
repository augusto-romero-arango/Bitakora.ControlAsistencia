using System.Globalization;
using System.Text;

namespace Bitakora.ControlAsistencia.ReadModels.Colaboradores;

/// <summary>
/// Read model del directorio de colaboradores (issue #587) -- vista propia para ENCONTRAR a una
/// persona concreta por nombre o numero de identificacion (MEF-ADR-0041), a diferencia de
/// FichaColaborador (consulta puntual por identificacion completa y exacta) y del listado de fichas
/// (busqueda por grupo: sede + etiquetas + fecha de referencia). Una entrada por colaborador.
/// </summary>
/// <remarks>
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el comportamiento
/// de proyeccion vive en la clase companion DirectorioColaboradorProjection, en el worker
/// (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels, la cuarta isla del repo
/// (CA-ADR-0029): cero referencias de proyecto.
///
/// Sin sufijo "View" (MEF-ADR-0041 decision 3, precedente FichaColaborador).
///
/// Id es el stream key que compone ColaboradorAggregateRoot.ComputarStreamId(Identificacion):
/// "{Tipo}-{Numero}" (ej. "CC-79879078") -- ES la identificacion completa, por eso NO existe un
/// campo Identificacion aparte (seria redundante con el Id, mismo criterio que fijo FichaColaborador
/// en su momento -- decision de refinamiento 2026-08-12, reafirmada para este issue).
///
/// TipoDocumento/NumeroDocumento son la forma DESCOMPUESTA de esa misma identidad -- las necesita el
/// filtro por numero suelto del issue hermano (#590): el asistente puede recibir solo "79879078" sin
/// el prefijo de tipo. NumeroDocumento se normaliza con <see cref="NormalizarNumeroDocumento"/>
/// (misma regla que Identificacion.Crear en Colaboradores.DomainEvents, duplicada deliberadamente
/// por MEF-ADR-0018 -- ver el issue, "cruza islas").
///
/// TokensNombre es la forma normalizada para buscar por nombre (minusculas, sin diacriticos, una
/// palabra por token) -- ver <see cref="TokenizarNombre"/>. NombreCompleto conserva la forma
/// original para display.
///
/// CodigoColaborador, VigenteDesde y VigenteHasta reflejan siempre la ULTIMA vinculacion -- igual
/// que FichaColaborador, un reingreso (VinculacionIniciada tras terminacion) sobrescribe estos tres
/// campos (y CodigoSede). Sin ShouldDelete: el directorio nunca borra -- los terminados siguen
/// apareciendo, la vigencia viaja y el consumidor decide (issue #587, "Receta").
///
/// VigenteHasta usa el CENTINELA <see cref="CentinelaVigenciaAbierta"/> cuando la vinculacion esta
/// abierta -- mismo valor que FichaColaborador.CentinelaVigenciaAbierta, pero constante PROPIA de
/// esta vista (cada vista codifica sus propios campos, decision de refinamiento del issue #587): los
/// dos procesos que la comparten (esta proyeccion al escribir, el endpoint de #590 al leer) solo se
/// referencian a traves de ReadModels.
///
/// CodigoSede refleja la sede de la VINCULACION vigente -- null si no tiene. Se asienta desde
/// SedeAsignada (reemplazo completo) y desde VinculacionIniciada.CodigoSede (un reingreso sin sede
/// LIMPIA el valor anterior -- "reingreso nace limpio", espejo de FichaColaborador/#520).
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
    /// <summary>
    /// Valor de <see cref="VigenteHasta"/> que significa "vinculacion abierta" (sin terminacion
    /// registrada). Mismo valor que FichaColaborador.CentinelaVigenciaAbierta, pero declarado aqui
    /// como constante PROPIA de esta vista -- ver remarks de la clase.
    /// </summary>
    public static readonly DateOnly CentinelaVigenciaAbierta = new(9999, 12, 31);

    /// <summary>
    /// Tokeniza un nombre para busqueda: minusculas, sin diacriticos, una palabra por token
    /// separando por todo caracter que no sea letra ni digito, sin vacios ni duplicados, en orden de
    /// aparicion. Expuesta por la vista (Tell-don't-Ask, MEF-ADR-0012) porque la usan DOS procesos
    /// que no se referencian entre si: esta proyeccion (al escribir TokensNombre) y el endpoint de
    /// #590 (al leer, para tokenizar el termino de busqueda del cliente).
    /// </summary>
    public static IReadOnlyList<string> TokenizarNombre(string nombre)
    {
        // Mismo patron que Etiqueta.Normalizar (Colaboradores.DomainEvents): descomponer en FormD,
        // filtrar las marcas de combinacion (NonSpacingMark) y recomponer en FormC, minusculas
        // invariantes. Duplicado deliberadamente (MEF-ADR-0018, cruza islas).
        var normalizado = new string(nombre.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        var tokens = new List<string>();
        var tokenActual = new StringBuilder();

        foreach (var caracter in normalizado)
        {
            if (char.IsLetterOrDigit(caracter))
            {
                tokenActual.Append(caracter);
                continue;
            }

            AgregarTokenSiNuevo(tokens, tokenActual);
        }

        AgregarTokenSiNuevo(tokens, tokenActual);

        return tokens;
    }

    private static void AgregarTokenSiNuevo(List<string> tokens, StringBuilder tokenActual)
    {
        if (tokenActual.Length == 0) return;

        var token = tokenActual.ToString();
        tokenActual.Clear();

        if (!tokens.Contains(token))
            tokens.Add(token);
    }

    /// <summary>
    /// Normaliza un numero de documento: conserva solo caracteres ASCII letra/digito, mayusculas
    /// invariantes -- misma regla que Identificacion.Crear (Colaboradores.DomainEvents), duplicada
    /// deliberadamente (MEF-ADR-0018, cruza islas: ReadModels no referencia DomainEvents). Expuesta
    /// por la vista por la misma razon que TokenizarNombre: la usan esta proyeccion y el endpoint de
    /// #590.
    /// </summary>
    public static string NormalizarNumeroDocumento(string numero) =>
        new string((numero ?? string.Empty)
            .Where(char.IsAsciiLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
