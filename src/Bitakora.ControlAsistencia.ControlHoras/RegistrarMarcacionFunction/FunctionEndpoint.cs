using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;

// 201 sin Location: la marcacion no tiene GET canonico ni id en el comando (MEF-ADR-0043 paso 1).
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("RegistrarMarcacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "control-horas/marcaciones")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<RegistrarMarcacion>(req, ct);
        if (error is not null)
            return error;

        // El duplicado silencioso termina en el mismo 201: el handler retorna sin excepcion
        // tanto si creo la marcacion como si ya existia.
        await commandRouter.InvokeAsync(comando!, ct);

        return new CreatedResult();
    }
}
