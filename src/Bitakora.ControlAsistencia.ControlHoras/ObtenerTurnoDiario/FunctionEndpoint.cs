using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoDiario;

// Issue #289: primera Function GET del BC (skills/projections/naming.md, MEF-ADR-0006 enmienda
// #363, via (a) proyeccion materializada). Feature folder sin sufijo Function, un namespace por
// query (skills/projections/read-apis.md): esta clase FunctionEndpoint no colisiona con las otras
// cuatro homonimas del ensamblado (RegistrarMarcacionFunction, AdicionarMarcacionCuando...,
// AsignarTurnoCuando...) porque cada una vive en su propio namespace.
//
// CA-5: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver (nunca a un
// tenant id de la ruta/query string, MEF-ADR-0028/skills/projections/read-apis.md -- mitigacion
// estructural contra BOLA/IDOR). empleadoId y fecha SI vienen de la ruta: son el recurso, no el
// tenant.
//
// FASE ROJA (projection-test-writer): Run es un stub. projection-implementer completa el
// session.LoadAsync<TurnoDiarioView>(streamKey), el mapeo a TurnoDiarioRespuesta (sin el Id, CA-5)
// y el 404 sin body cuando no hay turno vigente (CA-6). El constructor SI debe resolver limpio
// del contenedor -- eso es lo que verifica el test de composicion de ComposicionServiciosTests
// (CA-7), hermano de MEF-ADR-0029: no depende de que Run este implementado.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerTurnoDiario")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/turnos-diarios/{empleadoId}/{fecha}")]
        HttpRequest req,
        string empleadoId,
        string fecha,
        CancellationToken ct)
    {
        // Referencia minima a store/tenantResolver para evitar CS9113 (parametro de constructor
        // primario sin lectura) mientras Run no tiene implementacion real -- projection-implementer
        // reemplaza este cuerpo por el LoadAsync/mapeo/404 real (CA-5/CA-6).
        _ = store;
        _ = tenantResolver;
        throw new NotImplementedException();
    }
}
