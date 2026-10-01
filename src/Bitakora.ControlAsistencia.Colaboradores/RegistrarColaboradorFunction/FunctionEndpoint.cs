using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;

// Issue #330: endpoint HTTP POST para registrar un colaborador bajo control de asistencia.
// MEF-ADR-0006: [Function("RegistrarColaborador")] como convencion de nombrado; carpeta CON sufijo
// "Function" porque es un comando HTTP y el record del comando es homonimo del feature folder --
// las carpetas sin sufijo (ObtenerTurnoVigente/ListarTurnosVigentes) son queries GET, que no tienen
// record de comando con el que colisionar. Sin el sufijo, este archivo no podria nombrar su propio
// comando sin un alias de using.
// Route = "colaboradores" (kebab-case minusculo, MEF-ADR-0043 seccion 3 / issue #378 CA-5): antes
// "Colaboradores" (PascalCase) -- dominio y recurso son homonimos, un segundo segmento seria
// redundante; unico cambio, sin tocar el resto del endpoint.
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
