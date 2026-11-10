using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;

// Carpeta CON sufijo "Function": sin el, el namespace colisiona con el record homonimo del comando
// y este archivo no podria nombrarlo sin un alias de using.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("RegistrarColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "colaboradores")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<RegistrarColaborador>(req, ct);
        if (error is not null)
            return error;

        try
        {
            await commandRouter.InvokeAsync(comando!, ct);
        }
        catch (PrecondicionComandoException ex)
        {
            switch (ex)
            {
                case RecursoYaExisteException:
                    return new ConflictObjectResult(ex.Message);
                default:
                    throw;
            }
        }

        var identificacion = Identificacion.Crear(
            TipoIdentificacion.Desde(comando!.TipoIdentificacion), comando.NumeroIdentificacion);

        return new CreatedResult($"/api/colaboradores/fichas/{identificacion}", null);
    }
}
