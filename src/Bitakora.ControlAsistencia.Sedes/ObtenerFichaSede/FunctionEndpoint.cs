using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ObtenerFichaSede;

// Issue #461 (creacion): Function GET del read model FichaSede (via (a) proyeccion materializada,
// skills/projections/naming.md, MEF-ADR-0006). Feature folder sin sufijo Function, un namespace por
// query (skills/projections/read-apis.md).
//
// CA-5: consulta puntual por {codigo} de ruta -- 404 sin body cuando la ficha no existe. El
// COMPORTAMIENTO de Run (recomputar el stream key via SedeAggregateRoot.ComputarStreamId,
// session.LoadAsync, el 200/404) es responsabilidad de projection-implementer (MEF-ADR-0033, stub
// minimo de compilacion): este archivo solo fija el constructor (IDocumentStore, ITenantResolver)
// que el test de composicion (Sedes.Tests/Infraestructura/ComposicionServiciosTests.cs) resuelve
// desde el contenedor DI, mismo patron que ObtenerFichaColaborador/ObtenerTurnoVigente.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerFichaSede")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sedes/fichas/{codigo}")]
        HttpRequest req,
        string codigo,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
