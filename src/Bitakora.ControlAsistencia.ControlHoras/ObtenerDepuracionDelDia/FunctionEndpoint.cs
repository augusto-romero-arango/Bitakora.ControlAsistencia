using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ObtenerDepuracionDelDia;

// Function GET via (b1) -- aggregate en vivo, sin proyeccion materializada
// (skills/projections/read-apis.md, MEF-ADR-0035). Feature folder sin sufijo Function, un namespace
// por query (skills/projections/naming.md): esta clase FunctionEndpoint no colisiona con las demas
// del ensamblado porque cada una vive en su propio namespace.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    private const string FormatoFecha = "yyyy-MM-dd";

    [Function("ObtenerDepuracionDelDia")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/depuraciones/{codigoColaborador}/{fecha}")]
        HttpRequest req,
        string codigoColaborador,
        string fecha,
        CancellationToken ct)
    {
        // Parseo tipado explicito ANTES de tocar Marten (MEF-ADR-0037 seccion 2): 400 con mensaje,
        // nunca el BadRequestResult pelado.
        if (!DateOnly.TryParseExact(
                fecha, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaParseada))
            return new BadRequestObjectResult(
                $"El parametro 'fecha' debe tener el formato {FormatoFecha}");

        var streamId = DiaCalculadoAggregateRoot.ComputarStreamId(codigoColaborador, fechaParseada);

        // La QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver -- nunca a
        // un tenant id que llegara por ruta o query string (MEF-ADR-0028).
        await using var session = store.QuerySession(tenantResolver.TenantId);
        var dia = await session.Events.AggregateStreamAsync<DiaCalculadoAggregateRoot>(streamId, token: ct);

        // 404 sin body: nada creo la depuracion de ese dia, no es un error.
        if (dia is null)
            return new NotFoundResult();

        return new OkObjectResult(dia.GenerarDepuracionDelDia());
    }
}
