using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction;

// Issue #458: endpoint HTTP PUT para asignar (o reemplazar por completo) el centro de costos de una
// sede -- VO opaco direccionable por {codigo} (MEF-ADR-0043 paso 2). MEF-ADR-0006:
// [Function("AsignarCentroDeCostos")]; carpeta CON sufijo "Function".
// Route = "sedes/{codigo}/centro-de-costos" (kebab-case minusculo, MEF-ADR-0043 seccion 3):
// asignar por primera vez y reemplazar son el mismo comando (PUT semantico).
// CA-ADR-0030 / MEF-ADR-0004 (precedente ModificarNombreSedeFunction.FunctionEndpoint): validar
// {codigo} de ruta (400) -> validar body (400 via IRequestValidator) -> despachar comando ->
// KeyNotFoundException -> 404; exito -> 202 Accepted. Fase roja: stub minimo, el implementer
// completa la orquestacion real.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarCentroDeCostos")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "sedes/{codigo}/centro-de-costos")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoSedeDeRuta.EsValido(codigo, out var errorDeCodigo))
            return errorDeCodigo;

        var (body, error) = await requestValidator.ValidarAsync<AsignarCentroDeCostosBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new AsignarCentroDeCostos(codigo, body!.CentroDeCostos);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
