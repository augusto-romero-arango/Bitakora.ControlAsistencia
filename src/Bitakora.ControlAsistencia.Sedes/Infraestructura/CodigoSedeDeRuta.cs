using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// El {codigo} de ruta no pasa por IRequestValidator (que solo cubre el body): sin esta guarda, un
// segmento fuera del charset URL-safe llega crudo a ComputarStreamId y el 400 que exige
// MEF-ADR-0037 seccion 2 se degrada a un 404 por stream inexistente. Gemela de
// IdentificacionDeRuta (Colaboradores, #379).
public static class CodigoSedeDeRuta
{
    public static bool EsValido(string codigo, [NotNullWhen(false)] out IActionResult? error)
    {
        if (ValidacionesCompartidasSedes.EsCodigoSedeUrlSafe(codigo))
        {
            error = null;
            return true;
        }

        error = new BadRequestObjectResult(ValidacionesCompartidasSedes.MensajeCodigoSedeUrlSafe);
        return false;
    }
}
