using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoVigente;

// Issue #328: Function GET del read model TurnoVigente (via (a) proyeccion materializada,
// skills/projections/naming.md, MEF-ADR-0006 enmienda #363). Feature folder sin sufijo Function, un
// namespace por query (skills/projections/read-apis.md): esta clase FunctionEndpoint no colisiona
// con ObtenerTurnoDiario/ListarTurnosDiarios/RegistrarMarcacionFunction/... porque cada una vive en
// su propio namespace.
//
// FASE ROJA (projection-test-writer, issue #328): Run es un stub que lanza NotImplementedException
// a proposito -- el comportamiento real (parseo tipado de empleadoId/fecha con 400 explicito,
// ControlDiarioAggregateRoot.ComputarStreamId, QuerySession acotada al tenant de ITenantResolver,
// session.LoadAsync<TurnoVigente> y el 200/404, CA-4) es responsabilidad de projection-implementer.
// El constructor SI se prueba (ComposicionServiciosTests.AgregarServiciosControlHoras_
// ResuelveElEndpointDeObtenerTurnoVigente...): IDocumentStore e ITenantResolver ya resuelven del
// contenedor del write-side (los usa ObtenerTurnoDiario), asi que ese test de composicion no
// requiere implementar Run para quedar en verde -- solo prueba wiring, no comportamiento.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerTurnoVigente")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/turnos-vigentes/{empleadoId}/{fecha}")]
        HttpRequest req,
        string empleadoId,
        string fecha,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
