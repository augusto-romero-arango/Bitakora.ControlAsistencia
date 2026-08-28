using Bitakora.ControlAsistencia.ReadModels.Sedes;
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
// de asignabilidad.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarFichasSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sedes/fichas")]
        HttpRequest req,
        CancellationToken ct)
    {
        string? filtroActivaCrudo = req.Query["activa"];
        bool? filtroActiva = null;
        if (!string.IsNullOrEmpty(filtroActivaCrudo))
        {
            if (!bool.TryParse(filtroActivaCrudo, out var filtroActivaTipado))
                return new BadRequestObjectResult("El filtro 'activa' debe ser true o false");

            filtroActiva = filtroActivaTipado;
        }

        // CA-6/MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por query string.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        IQueryable<FichaSede> query = session.Query<FichaSede>();
        if (filtroActiva is not null)
            query = query.Where(f => f.Activa == filtroActiva);

        // OrderBy(Codigo): sin este orden, dos consultas consecutivas podrian devolver el mismo
        // catalogo permutado (mismo criterio que ListarCategoriasDeEtiquetas).
        var fichas = await query.OrderBy(f => f.Codigo).ToListAsync(ct);

        return new OkObjectResult(fichas);
    }
}
