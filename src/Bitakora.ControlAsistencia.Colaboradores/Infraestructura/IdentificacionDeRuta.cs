using System.Diagnostics.CodeAnalysis;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

// Issue #379 (refactor de revision): traduccion del {id} de ruta a Identificacion en el borde HTTP
// -- el par "parsear una vez + 400 explicito si falla" que MEF-ADR-0037 seccion 2 exige, en un solo
// sitio.
//
// El bloque try/catch con su literal de mensaje estaba copiado en SEIS endpoints (#376 AsignarEtiqueta
// -- via body --, #377 CorregirNombres, #378 IniciarVinculacion, #386 ObtenerFichaColaborador y los
// tres que agrego el issue #379: TerminarVinculacion, AnularTerminacion,
// CorregirFechaInicioVinculacion). Con seis copias del MISMO literal, un cambio de redaccion en uno
// solo compila y pasa todos los tests, dejando dos textos distintos para el mismo 400.
//
// MEF-ADR-0018: la Rule of Three regula reglas del DOMINIO que pueden divergir; esta es mecanica
// neutral del borde (el mismo parseo, el mismo codigo HTTP, para cualquier endpoint que reciba
// {id}), que ese ADR deja explicitamente fuera de su alcance -- mismo razonamiento con el que
// ValidacionesCompartidas (#387) centralizo el charset URL-safe del codigo.
//
// Forma TryParsear (patron TryParse del BCL) en vez de tupla: el call site queda en un guard clause
// de dos lineas sin operador null-forgiving, porque los atributos de nullable flow le dicen al
// compilador que la identificacion es no-nula cuando retorna true.
public static class IdentificacionDeRuta
{
    /// <summary>
    /// Traduce el segmento {id} de la ruta ("CC-79543210") a <see cref="Identificacion"/>.
    /// Retorna false y un 400 con mensaje cuando el texto no parsea (sin guion, tipo fuera de la
    /// lista cerrada PILA, o numero vacio tras la limpieza) -- las tres causas que
    /// Identificacion.Parsear senala con ArgumentException.
    /// </summary>
    public static bool TryParsear(
        string id,
        [MaybeNullWhen(false)] out Identificacion identificacion,
        [NotNullWhen(false)] out IActionResult? error)
    {
        try
        {
            identificacion = Identificacion.Parsear(id);
            error = null;
            return true;
        }
        catch (ArgumentException)
        {
            identificacion = null;
            error = new BadRequestObjectResult(
                "El id de la ruta es invalido -- debe tener la forma {Tipo}-{Numero}");
            return false;
        }
    }
}
