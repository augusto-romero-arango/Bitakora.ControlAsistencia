using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

// Function QUERY (RFC 10008, MEF-ADR-0042) sobre la vista materializada AsistenciaDiaria, via (a')
// de MEF-ADR-0035.
//
// Primer endpoint QUERY de este consumidor: los gates NO VERIFICADO de MEF-ADR-0042 seccion 6
// (front-end de App Service y APIM reenviando un verbo no estandar) solo los cierra el smoke test
// contra dev despues del deploy -- un 404 en dev puede significar tanto "no desplegado" como "el
// borde filtro el verbo".
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarAsistenciasDiarias")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/asistencias-diarias")]
        HttpRequest req,
        CancellationToken ct)
    {
        // El 415 va ANTES de leer el body: ante un Content-Type no-JSON, ReadFromJsonAsync lanza
        // una excepcion que NO es JsonException y escaparia como 500 pese al catch de abajo.
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

        // Obligatorio, a diferencia del filtro homonimo de ListarTurnosVigentes: esta es la
        // pantalla de UN colaborador.
        if (string.IsNullOrWhiteSpace(filtro.CodigoColaborador))
            return new ObjectResult("CodigoColaborador es obligatorio")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (filtro.DesdeFecha is null || filtro.HastaFecha is null)
            return new ObjectResult("DesdeFecha y HastaFecha son obligatorios")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (filtro.DesdeFecha > filtro.HastaFecha)
            return new ObjectResult("DesdeFecha no puede ser posterior a HastaFecha")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        var codigoColaborador = filtro.CodigoColaborador;
        var desde = filtro.DesdeFecha.Value;
        var rangoAplicado = RangoConsulta.Recortar(desde, filtro.HastaFecha.Value);

        // Sesion acotada al tenant que resuelve ITenantResolver, nunca a un dato de la request
        // (mitigacion estructural contra BOLA/IDOR, MEF-ADR-0028).
        await using var session = store.QuerySession(tenantResolver.TenantId);

        var documentos = await session.Query<AsistenciaDiaria>()
            .Where(a => a.CodigoColaborador == codigoColaborador
                        && a.Fecha >= desde
                        && a.Fecha <= rangoAplicado.HastaAplicado)
            .ToListAsync(ct);

        var filas = SintesisCalendarioAsistencia.Completar(desde, rangoAplicado.HastaAplicado, documentos);

        // Nunca 404: un rango sin documentos son filas sinteticas, no un error.
        return new OkObjectResult(new ListaAsistenciasDiarias(
            desde, rangoAplicado.HastaAplicado, rangoAplicado.RangoRecortado, filas));
    }
}
