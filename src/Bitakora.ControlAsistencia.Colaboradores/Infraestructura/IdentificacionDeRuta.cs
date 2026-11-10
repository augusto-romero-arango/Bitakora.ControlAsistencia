using System.Diagnostics.CodeAnalysis;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

// Sitio unico del par "parsear el {id} de ruta una vez + 400 explicito si falla" (MEF-ADR-0037
// seccion 2) para todos los endpoints que reciben {id}: copias del literal divergirian sin que
// ningun test lo note.
public static class IdentificacionDeRuta
{
    /// <summary>
    /// Traduce el segmento {id} de la ruta ("CC-79543210"); retorna false y un 400 cuando no parsea.
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
