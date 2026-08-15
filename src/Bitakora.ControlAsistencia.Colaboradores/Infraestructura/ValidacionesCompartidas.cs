using System.Text.RegularExpressions;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

// Issue #387: CodigoColaborador debe ser URL-safe porque viajara como segmento de ruta
// (vinculaciones/{codigo}:terminar, issue #379). Set permitido: unreserved characters de RFC 3986
// seccion 2.3 (A-Z a-z 0-9 - . _ ~) -- el mismo set que las Microsoft Azure REST API Guidelines
// fijan para path segments (MEF-ADR-0043 seccion 1). El ":" queda explicitamente fuera (reservado
// como separador de accion). Se RECHAZA (400), nunca se limpia ni normaliza en silencio -- a
// diferencia de Identificacion (#381), el codigo lo asigna la empresa y alterarlo cambiaria un dato
// ajeno.
//
// La regla aparece en dos validators (RegistrarColaboradorValidator, IniciarVinculacionBodyValidator
// -- issue #378 renombro al segundo desde ReingresarColaboradorValidator) y se define en un solo
// lugar por exigencia del CA-4 del issue original (#387). Con dos sitios, la Rule of Three de
// MEF-ADR-0018 toleraria la duplicacion, pero esa heuristica regula reglas del DOMINIO que pueden
// divergir: aqui el set de caracteres es mecanica neutral (una precondicion de la URL, identica para
// cualquier comando que reciba el codigo), que ese mismo ADR deja fuera de su alcance.
public static partial class ValidacionesCompartidas
{
    // Anclas \A y \z (no ^ y $): en .NET el ancla "$" hace match tambien ANTES de un "\n" final,
    // asi que "^...$" aceptaria "COL-001\n" -- un valor que rompe la URL igual que un espacio y que
    // ademas habilita CRLF injection en cualquier consumidor que lo reenvie en un header o lo
    // escriba en un log. "\z" ancla al fin real de la cadena, sin esa excepcion.
    [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z")]
    private static partial Regex CodigoColaboradorUrlSafeRegex();

    // Mensaje explicito en vez del default de FluentValidation ("is not in the correct format"):
    // este texto viaja al cliente dentro del ValidationProblemDetails del 400 (RequestValidator), y
    // el default no le dice al integrador cual es el formato esperado. Mismo criterio que la regla
    // de TipoIdentificacion en RegistrarColaboradorValidator (issue #378: IniciarVinculacionBody ya
    // no valida TipoIdentificacion -- se deriva de {id} en la ruta via Identificacion.Parsear).
    public static IRuleBuilderOptions<T, string> DebeSerCodigoColaboradorUrlSafe<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .Matches(CodigoColaboradorUrlSafeRegex())
            .WithMessage(
                "El codigo del colaborador solo admite letras sin tilde, digitos y los caracteres - . _ ~");
}
