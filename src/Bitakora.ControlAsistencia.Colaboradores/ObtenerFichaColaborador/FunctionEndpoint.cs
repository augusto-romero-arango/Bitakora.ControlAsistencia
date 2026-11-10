using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;

// {id} es la misma llave que FichaColaborador.Id: consulta y comando comparten forma de id.
// INCLUYE no vigentes (sin filtro de vigencia, a diferencia del listado).
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ObtenerFichaColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "colaboradores/fichas/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var streamKey = ColaboradorAggregateRoot.ComputarStreamId(identificacion);

        // Siempre el tenant de ITenantContext, nunca uno que llegue por ruta o query (BOLA/IDOR).
        await using var session = store.QuerySession(tenantContext.TenantId);
        var ficha = await session.LoadAsync<FichaColaborador>(streamKey, ct);

        if (ficha is null)
            return new NotFoundResult();

        return new OkObjectResult(FichaColaboradorRespuesta.DesdeVista(ficha));
    }
}

// DTO de excepcion (MEF-ADR-0041 decision 4): existe solo para que el centinela de vigencia
// abierta (9999-12-31), estructura interna del read model, jamas salga por la API.
public sealed record FichaColaboradorRespuesta(
    string Id,
    string NombreCompleto,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<EtiquetaFicha> Etiquetas,
    IReadOnlyDictionary<string, string> EtiquetasNormalizadas,
    string? CodigoSede = null,
    Guid? JornadaId = null)
{
    // El centinela sale de FichaColaborador.CentinelaVigenciaAbierta, nunca de un literal aqui: lo
    // escribe el worker en otro proceso, y con dos literales un cambio de un solo lado compila, pasa
    // los tests y filtra 9999-12-31 por la API.
    public static FichaColaboradorRespuesta DesdeVista(FichaColaborador ficha) =>
        new(
            ficha.Id,
            ficha.NombreCompleto,
            ficha.CodigoColaborador,
            ficha.VigenteDesde,
            ficha.VigenteHasta == FichaColaborador.CentinelaVigenciaAbierta ? null : ficha.VigenteHasta,
            ficha.Etiquetas,
            ficha.EtiquetasNormalizadas,
            ficha.CodigoSede,
            ficha.JornadaId);
}
