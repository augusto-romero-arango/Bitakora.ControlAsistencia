using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
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
// CA-5: consulta puntual por {codigo} de ruta -- 404 sin body cuando la ficha no existe.
//
// MEF-ADR-0037 seccion 2: el {codigo} de ruta no pasa por IRequestValidator (que solo cubre el
// body de comandos) -- CodigoSedeDeRuta.EsValido es el mismo punto unico de conversion que ya usan
// los comandos del ciclo de vida de la sede (ActivarSedeFunction, etc.): rechaza con 400 antes de
// tocar Marten, y SedeAggregateRoot.ComputarStreamId(codigo) es la unica forma de construir el
// stream key -- nunca una concatenacion propia del endpoint.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerFichaSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sedes/fichas/{codigo}")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoSedeDeRuta.EsValido(codigo, out var errorDeCodigo))
            return errorDeCodigo;

        var streamKey = SedeAggregateRoot.ComputarStreamId(codigo);

        // CA-5/MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por ruta o query string.
        await using var session = store.QuerySession(tenantResolver.TenantId);
        var ficha = await session.LoadAsync<FichaSede>(streamKey, ct);

        return ficha is null ? new NotFoundResult() : new OkObjectResult(ficha);
    }
}
