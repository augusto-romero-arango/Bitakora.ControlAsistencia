using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

// Issue #428: Function QUERY (RFC 10008, MEF-ADR-0042) sobre la vista materializada AsistenciaDiaria
// (#426), via (a') de MEF-ADR-0035, con agregacion en query-time (AgregadorResumenAsistencia) --
// este issue NO crea proyeccion, read model ni lifecycle nuevos.
//
// Fase roja (projection-test-writer): Run() hoy SOLO lanza NotImplementedException (MEF-ADR-0033,
// stub minimo de compilacion). El COMPORTAMIENTO completo (415/400/422, keyset por
// CodigoColaborador, recorte de rango, agregacion, sintesis de la fila pedida sin datos) es
// responsabilidad de projection-implementer.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarResumenesAsistencia")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/resumenes-asistencia")]
        HttpRequest req,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
