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
    [Function("ObtenerDepuracionDelDia")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/depuraciones/{codigoColaborador}/{fecha}")]
        HttpRequest req,
        string codigoColaborador,
        string fecha,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
