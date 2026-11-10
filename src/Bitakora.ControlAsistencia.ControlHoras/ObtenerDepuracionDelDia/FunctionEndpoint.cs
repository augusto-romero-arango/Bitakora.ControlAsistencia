using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ObtenerDepuracionDelDia;

// Lee el aggregate en vivo, sin proyeccion materializada (MEF-ADR-0035).
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
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
        if (!DateOnly.TryParseExact(
                fecha, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaParseada))
            return new BadRequestObjectResult(
                $"El parametro 'fecha' debe tener el formato {FormatoFecha}");

        var streamId = DiaCalculadoAggregateRoot.ComputarStreamId(codigoColaborador, fechaParseada);

        // Sesion acotada al tenant que resuelve ITenantContext, nunca a un dato de la request (MEF-ADR-0028).
        await using var session = store.QuerySession(tenantContext.TenantId);
        var dia = await session.Events.AggregateStreamAsync<DiaCalculadoAggregateRoot>(streamId, token: ct);

        // 404 sin body: nada creo la depuracion de ese dia, no es un error.
        if (dia is null)
            return new NotFoundResult();

        return new OkObjectResult(dia.GenerarDepuracionDelDia());
    }
}
