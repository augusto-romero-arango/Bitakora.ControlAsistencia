using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
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
// Guard 415/400/422 identico al ejemplo canonico QUERY de skills/projections/read-apis.md: 415
// ANTES de leer el body (HasJsonContentType), 400 si el body no es JSON valido (catch JsonException
// -- ReadFromJsonAsync lanza una excepcion que NO es JsonException cuando el Content-Type no es
// JSON conocido, de ahi el guard 415 previo), 422 cuando el JSON es valido pero su contenido no es
// procesable (CodigoColaborador ausente/vacio, fechas ausentes, rango invertido -- CA-4).
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarAsistenciasDiarias")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/asistencias-diarias")]
        HttpRequest req,
        CancellationToken ct)
    {
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        FiltroListarAsistenciasDiarias? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarAsistenciasDiarias>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        // CA-4: CodigoColaborador obligatorio -- pantalla de UN colaborador (issue #427, "Contexto").
        if (string.IsNullOrWhiteSpace(filtro.CodigoColaborador))
            return new ObjectResult("CodigoColaborador es obligatorio")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (filtro.DesdeFecha is null || filtro.HastaFecha is null)
            return new ObjectResult("DesdeFecha y HastaFecha son obligatorios")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (filtro.DesdeFecha > filtro.HastaFecha)
            return new ObjectResult("DesdeFecha no puede ser posterior a HastaFecha")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        var desde = filtro.DesdeFecha.Value;
        var hasta = filtro.HastaFecha.Value;

        // CA-3: recorte de la cota de 31 dias, siempre hacia adelante desde `desde`.
        var rangoAplicado = RangoConsulta.Recortar(desde, hasta);

        // CA-6: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
        // nunca a un tenant id que llegara por el body (mitigacion estructural contra BOLA/IDOR,
        // MEF-ADR-0028/skills/projections/read-apis.md). CodigoColaborador/DesdeFecha/HastaFecha SI
        // vienen del body: son el filtro del recurso, no el tenant.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // Via (a') de MEF-ADR-0035: session.Query<TView>() sobre la proyeccion materializada
        // AsistenciaDiaria (#426). Este issue no crea proyeccion ni toca el worker.
        var documentos = await session.Query<AsistenciaDiaria>()
            .Where(a => a.CodigoColaborador == filtro.CodigoColaborador
                        && a.Fecha >= desde
                        && a.Fecha <= rangoAplicado.HastaAplicado)
            .ToListAsync(ct);

        // CA-1/CA-2/CA-5: el calendario completo -- una fila por cada dia del rango aplicado, dias
        // sin documento sintetizados -- lo produce la funcion pura, no la consulta LINQ.
        var filas = SintesisCalendarioAsistencia.Completar(desde, rangoAplicado.HastaAplicado, documentos);

        var respuesta = new ListaAsistenciasDiarias(
            desde, rangoAplicado.HastaAplicado, rangoAplicado.RangoRecortado, filas);

        // Nunca 404 (CA-5): un rango sin documentos son 31 filas sinteticas, no un error.
        return new OkObjectResult(respuesta);
    }
}
