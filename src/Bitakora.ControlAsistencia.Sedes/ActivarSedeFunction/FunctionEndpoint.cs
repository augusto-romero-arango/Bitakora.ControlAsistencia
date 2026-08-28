using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction;

// Accion de negocio con verbo propio: ni crea, ni reemplaza un VO direccionable, ni remueve
// (MEF-ADR-0043 paso 4) -- POST "{recurso}:{verbo}", nunca PUT sobre una bandera. Sin body, asi
// que no hay IRequestValidator: el {codigo} de ruta es lo unico que validar (MEF-ADR-0037
// seccion 2).
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("ActivarSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sedes/{codigo}:activar")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoSedeDeRuta.EsValido(codigo, out var errorDeCodigo))
            return errorDeCodigo;

        var comando = new ActivarSede(codigo);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
