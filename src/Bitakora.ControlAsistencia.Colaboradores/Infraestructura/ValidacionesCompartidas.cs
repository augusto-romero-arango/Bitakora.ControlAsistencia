using System.Text.RegularExpressions;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

// CodigoColaborador viaja como segmento de ruta (vinculaciones/{codigo}:terminar), asi que debe ser
// URL-safe: unreserved characters de RFC 3986 seccion 2.3 (A-Z a-z 0-9 - . _ ~), MEF-ADR-0043
// seccion 1; el ":" queda fuera (separador de accion). Se RECHAZA (400), nunca se limpia: a
// diferencia de Identificacion, el codigo lo asigna la empresa y alterarlo cambiaria un dato ajeno.
public static partial class ValidacionesCompartidas
{
    // \A y \z, no ^ y $: en .NET "$" tambien hace match ANTES de un "\n" final, asi que "^...$"
    // aceptaria "COL-001\n" (rompe la URL y habilita CRLF injection en headers o logs).
    [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z")]
    private static partial Regex CodigoColaboradorUrlSafeRegex();

    // Mensaje explicito: viaja al cliente en el 400 y el default de FluentValidation ("is not in the
    // correct format") no le dice al integrador cual es el formato esperado.
    public static IRuleBuilderOptions<T, string> DebeSerCodigoColaboradorUrlSafe<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .Matches(CodigoColaboradorUrlSafeRegex())
            .WithMessage(
                "El codigo del colaborador solo admite letras sin tilde, digitos y los caracteres - . _ ~");
}
