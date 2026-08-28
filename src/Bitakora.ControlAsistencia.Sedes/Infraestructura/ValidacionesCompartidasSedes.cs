using System.Text.RegularExpressions;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// CodigoSede debe ser URL-safe: unreserved characters de RFC 3986 seccion 2.3 (A-Z a-z 0-9 - . _ ~),
// el mismo set que fija MEF-ADR-0043 seccion 1.1 para path segments. El ":" queda fuera (separador
// de la anatomia del stream, CA-ADR-0031). Se RECHAZA (400), nunca se normaliza en silencio
// (MEF-ADR-0043 seccion 1.2, caso "identificador asignado por un tercero": el codigo lo asigna la
// empresa, alterarlo cambiaria un dato ajeno).
public static partial class ValidacionesCompartidasSedes
{
    public const string MensajeCodigoSedeUrlSafe =
        "El codigo de la sede solo admite letras sin tilde, digitos y los caracteres - . _ ~";

    // Anclas \A y \z (no ^ y $): en .NET "$" hace match tambien ANTES de un "\n" final, asi que
    // "^...$" aceptaria "SEDE-001\n" -- un valor que rompe la URL y habilita CRLF injection en
    // cualquier consumidor que lo reenvie en un header o lo escriba en un log.
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
