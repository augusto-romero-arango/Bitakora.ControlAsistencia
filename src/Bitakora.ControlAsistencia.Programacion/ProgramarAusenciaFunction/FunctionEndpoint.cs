using System.Text.RegularExpressions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction;

// Crea una entidad con identidad propia -> 201 sin Location: la lectura de ausencias aun no existe
// (MEF-ADR-0043 paso 1; CA-ADR-0035).
public partial class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    // \z y no $: "$" acepta un "\n" final. Mismo charset URL-safe que la invariante de #387.
    [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z")]
    private static partial Regex CodigoUrlSafe();

    [Function(nameof(ProgramarAusencia))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/colaboradores/{codigo}/ausencias")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoUrlSafe().IsMatch(codigo ?? string.Empty))
            return new BadRequestObjectResult(
                "El codigo del colaborador solo admite letras sin tilde, digitos y los caracteres - . _ ~");

        var (body, error) = await requestValidator.ValidarAsync<ProgramarAusenciaBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new ProgramarAusencia(
            body!.Id, codigo!, body.Identificacion, body.NombreCompleto,
            body.FechaInicio, body.FechaFin, body.Motivo);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (PrecondicionComandoException ex)
        {
            switch (ex)
            {
                case RecursoYaExisteException:
                case ReglaDeNegocioDeclinadaException:
                    return new ConflictObjectResult(ex.Message);
                default:
                    throw;
            }
        }

        return new CreatedResult();
    }
}
