using System.Text.RegularExpressions;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// Issue #456 (gemela de ValidacionesCompartidas de Colaboradores, #387): CodigoSede debe ser
// URL-safe -- unreserved characters de RFC 3986 seccion 2.3 (A-Z a-z 0-9 - . _ ~), el mismo set que
// las Microsoft Azure REST API Guidelines fijan para path segments (MEF-ADR-0043 seccion 1). El
// ":" queda fuera del set (separador de la anatomia del stream, CA-ADR-0031). Se RECHAZA (400),
// nunca se limpia ni normaliza en silencio -- MEF-ADR-0043 seccion 1.2, caso "identificador
// asignado por un tercero": el codigo lo asigna la empresa, alterarlo cambiaria un dato ajeno.
public static partial class ValidacionesCompartidasSedes
{
    // Anclas \A y \z (no ^ y $): en .NET "$" hace match tambien ANTES de un "\n" final, asi que
    // "^...$" aceptaria "SEDE-001\n" -- un valor que rompe la URL y habilita CRLF injection en
    // cualquier consumidor que lo reenvie en un header o lo escriba en un log.
    [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z")]
    private static partial Regex CodigoSedeUrlSafeRegex();

    public static IRuleBuilderOptions<T, string> DebeSerCodigoSedeUrlSafe<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .Matches(CodigoSedeUrlSafeRegex())
            .WithMessage(
                "El codigo de la sede solo admite letras sin tilde, digitos y los caracteres - . _ ~");
}
