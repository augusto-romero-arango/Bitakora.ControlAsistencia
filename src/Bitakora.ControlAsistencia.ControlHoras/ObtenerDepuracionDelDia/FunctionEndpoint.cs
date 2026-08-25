using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ObtenerDepuracionDelDia;

// Issue #429: Function GET via (b1) -- aggregate en vivo, sin proyeccion materializada
// (skills/projections/read-apis.md, MEF-ADR-0035). Feature folder sin sufijo Function, un namespace
// por query (skills/projections/naming.md): esta clase FunctionEndpoint no colisiona con las demas
// del ensamblado porque cada una vive en su propio namespace.
//
// Fase roja (projection-test-writer): el cuerpo de Run es responsabilidad de
// projection-implementer -- parseo de fecha con TryParseExact y 400 con mensaje (CA-5),
// DiaCalculadoAggregateRoot.ComputarStreamId (MEF-ADR-0037, nunca una concatenacion propia del
// endpoint), session.Events.AggregateStreamAsync sobre una QuerySession acotada al tenant que
// resuelve ITenantResolver (CA-7, MEF-ADR-0028), y 404 sin body cuando el stream no existe (CA-6) o
// 200 con la vista que produce DiaCalculadoAggregateRoot.GenerarDepuracionDelDia() (CA-1).
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
        // CA-5: parseo tipado explicito ANTES de tocar Marten (MEF-ADR-0037 seccion 2) -- 400 con
        // mensaje, nunca el BadRequestResult pelado.
        if (!DateOnly.TryParseExact(
                fecha, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaParseada))
            return new BadRequestObjectResult(
                $"El parametro 'fecha' debe tener el formato {FormatoFecha}");

        var streamId = DiaCalculadoAggregateRoot.ComputarStreamId(codigoColaborador, fechaParseada);

        // CA-7: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
        // nunca a un tenant id que llegara por ruta o query string (MEF-ADR-0028).
        await using var session = store.QuerySession(tenantResolver.TenantId);
        var dia = await session.Events.AggregateStreamAsync<DiaCalculadoAggregateRoot>(streamId, token: ct);

        // CA-6: 404 sin body cuando el stream no existe -- ningun dato creo la depuracion de ese dia.
        if (dia is null)
            return new NotFoundResult();

        return new OkObjectResult(dia.GenerarDepuracionDelDia());
    }
}
