using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

// Issue #427: Function QUERY (RFC 10008, MEF-ADR-0042) sobre la vista materializada
// AsistenciaDiaria (#426), via (a') de MEF-ADR-0035 -- session.Query<AsistenciaDiaria>(). Este
// issue NO crea proyeccion ni toca el worker (issue #427, "Necesidad de lectura"): compone el
// filtro tipado del body, el recorte de rango (RangoConsulta) y la sintesis del calendario
// completo (SintesisCalendarioAsistencia) en el envelope de respuesta.
//
// Primer QUERY desplegado de este consumidor (issue #427, "Notas tecnicas"): el trigger "query"
// esta verificado por POC del marco contra .NET 10 + Azure Functions Core Tools 4.6.0
// (skills/projections/read-apis.md), pero projection-implementer debe reconfirmarlo contra Core
// Tools de este repo antes del primer despliegue.
//
// Stub de fase roja (projection-test-writer): el cuerpo real -- guard 415/400/422
// (HasJsonContentType + catch JsonException), CodigoColaborador/fechas obligatorios, rango
// invertido, apertura de QuerySession acotada al tenant del resolver (nunca a un tenant de la
// request), consulta LINQ y composicion del envelope de respuesta -- es responsabilidad de
// projection-implementer (skills/projections/read-apis.md, ejemplo canonico QUERY).
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarAsistenciasDiarias")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/asistencias-diarias")]
        HttpRequest req,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
