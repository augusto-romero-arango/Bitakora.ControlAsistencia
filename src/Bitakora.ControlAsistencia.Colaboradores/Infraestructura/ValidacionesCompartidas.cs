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
// La regla aparece en dos validators (RegistrarColaboradorValidator, ReingresarColaboradorValidator)
// -- MEF-ADR-0018 (Rule of Three): compartir la definicion unica evita que diverjan.
public static partial class ValidacionesCompartidas
{
    [GeneratedRegex("^[A-Za-z0-9._~-]+$")]
    private static partial Regex CodigoColaboradorUrlSafeRegex();

    public static IRuleBuilderOptions<T, string> DebeSerCodigoColaboradorUrlSafe<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder.Matches(CodigoColaboradorUrlSafeRegex());
}
