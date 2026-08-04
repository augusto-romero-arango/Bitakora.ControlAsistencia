using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosDiarios;

// Issue #290: segunda Function GET del BC (skills/projections/naming.md: Listar{X}s por
// filtro/lista), sobre la MISMA vista materializada TurnoDiarioView que ya usa ObtenerTurnoDiario
// (#289) -- ninguna proyeccion nueva, ningun cambio al seam del worker. Feature folder propio (un
// namespace por query, skills/projections/read-apis.md y naming.md): esta clase FunctionEndpoint
// no colisiona con las demas homonimas del ensamblado porque cada una vive en su propio namespace.
//
// Ruta sin {id}: reutiliza el mismo segmento de recurso "control-horas/turnos-diarios" que
// ObtenerTurnoDiario (naming.md: "una query reutiliza el mismo segmento de recurso que ya usa el
// comando/query de ese recurso"). No hay colision de plantilla verificada en la investigacion del
// planner: no existe ningun POST sobre este mismo segmento en este dominio.
//
// CA-1: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver -- nunca a
// un tenant id que llegue en la ruta o el query string (MEF-ADR-0028/skills/projections/read-apis.md,
// mitigacion estructural contra BOLA/IDOR). empleadoId/desde/hasta SI vienen del query string: son
// el filtro del recurso, no el tenant.
//
// FASE ROJA (projection-test-writer): Run es un stub. projection-implementer completa el parseo de
// desde/hasta/empleadoId desde el query string, la consulta session.Query<TurnoDiarioView>() (via
// (a'), read-apis.md), el recorte de rango via RangoConsulta.Recortar (CA-3/CA-4) y el mapeo a
// ListaTurnosDiarios (CA-5/CA-6). El constructor SI debe resolver limpio del contenedor -- eso es lo
// que verifica el test de composicion de ComposicionServiciosTests (CA-7), hermano de MEF-ADR-0029:
// no depende de que Run este implementado.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarTurnosDiarios")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/turnos-diarios")]
        HttpRequest req,
        CancellationToken ct)
    {
        // Referencia minima a store/tenantResolver para evitar CS9113 (parametro de constructor
        // primario sin lectura) mientras Run no tiene implementacion real -- projection-implementer
        // reemplaza este cuerpo por la consulta/recorte/mapeo real (CA-1 a CA-6).
        _ = store;
        _ = tenantResolver;
        throw new NotImplementedException();
    }
}
