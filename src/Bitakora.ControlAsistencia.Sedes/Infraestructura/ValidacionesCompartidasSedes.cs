using System.Text.RegularExpressions;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// CodigoSede URL-safe: unreserved de RFC 3986 seccion 2.3 (MEF-ADR-0043 seccion 1.1). El ":" queda
// fuera porque separa la anatomia del stream (CA-ADR-0031). Se rechaza con 400, nunca se normaliza:
// el codigo lo asigna la empresa (MEF-ADR-0043 seccion 1.2).
public static partial class ValidacionesCompartidasSedes
{
    public const string MensajeCodigoSedeUrlSafe =
        "El codigo de la sede solo admite letras sin tilde, digitos y los caracteres - . _ ~";

    // \A y \z, no ^ y $: en .NET "$" tambien casa antes de un "\n" final y "^...$" aceptaria
    // "SEDE-001\n", que rompe la URL y habilita CRLF injection.
    [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z")]
    private static partial Regex CodigoSedeUrlSafeRegex();

    public static bool EsCodigoSedeUrlSafe(string? codigo) =>
        !string.IsNullOrEmpty(codigo) && CodigoSedeUrlSafeRegex().IsMatch(codigo);

    public static IRuleBuilderOptions<T, string> DebeSerCodigoSedeUrlSafe<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .Matches(CodigoSedeUrlSafeRegex())
            .WithMessage(MensajeCodigoSedeUrlSafe);
}
