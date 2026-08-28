using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ListarFichasSede;

// Issue #461 (creacion): Function GET de listado sobre FichaSede -- mismo segmento de recurso que
// ObtenerFichaSede ("sedes/fichas"), sin QUERY: el filtro Activa es un unico par campo=valor en
// igualdad (MEF-ADR-0042 seccion 1), y SIN paginacion (decision de sesion 2026-08-27: coleccion
// acotada, Rule of Three si un cliente llega con miles -- MEF-ADR-0018).
//
// CA-6: sin filtro devuelve todas las fichas; "?activa=true"/"?activa=false" filtra por la bandera
// de asignabilidad. El COMPORTAMIENTO de Run (lectura del query string, session.Query, el 200 con
// la lista) es responsabilidad de projection-implementer (MEF-ADR-0033, stub minimo de
// compilacion): este archivo solo fija el constructor (IDocumentStore, ITenantResolver) que el test
// de composicion (Sedes.Tests/Infraestructura/ComposicionServiciosTests.cs) resuelve desde el
// contenedor DI.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarFichasSede")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sedes/fichas")]
        HttpRequest req,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
