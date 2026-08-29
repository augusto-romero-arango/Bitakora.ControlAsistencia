using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;

// Issue #489 (MEF-ADR-0043 paso 4): accion de negocio con verbo propio -- aprobar el dia completo,
// nunca un create/replace/remove. Route = "control-horas/depuraciones/{codigoColaborador}/{fecha}:aprobar",
// mismo formato de fecha que el GET existente (ObtenerDepuracionDelDia.FunctionEndpoint).
// CA-ADR-0030: el aggregate declina con resultado; este handler lo traduce a
// InvalidOperationException (-> 409). Sin consumidores downstream (event-sourcing puro) y sin caso
// 404: el aval del vacio (CA-7) hace que aprobar un dia sin stream tambien sea un acto valido.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    private const string FormatoFecha = "yyyy-MM-dd";

    [Function("AprobarDia")]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "control-horas/depuraciones/{codigoColaborador}/{fecha}:aprobar")]
        HttpRequest req,
        string codigoColaborador,
        string fecha,
        CancellationToken ct)
    {
        // Parseo tipado explicito ANTES de despachar el comando (MEF-ADR-0037 seccion 2): 400 con
        // mensaje, mismo criterio que ObtenerDepuracionDelDia.FunctionEndpoint.
        if (!DateOnly.TryParseExact(
                fecha, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaParseada))
            return new BadRequestObjectResult(
                $"El parametro 'fecha' debe tener el formato {FormatoFecha}");

        var (body, error) = await requestValidator.ValidarAsync<AprobarDiaBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new AprobarDia(codigoColaborador, fechaParseada, body!.Decisiones ?? []);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
