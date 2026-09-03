using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ListarDirectorioColaboradores;

// Issue #590: QUERY colaboradores/directorio (MEF-ADR-0042) sobre la vista DirectorioColaborador
// que #587 ya materializa -- via (a') (session.Query<DirectorioColaborador>()), segunda mitad del
// desglose de #587. Mismo patron que ListarFichasColaborador (#373): NO crea proyeccion ni read
// model nuevos, solo consulta la vista y suma el par de config-tests de compatibilidad de columna
// de version (CA-5, MEF-ADR-0034 seccion 6).
//
// Stub minimo de compilacion (MEF-ADR-0033, fase roja de projection-test-writer): Run() y
// DesdeVista() SOLO lanzan NotImplementedException. El comportamiento real (415/400/422,
// clasificacion de "identificaciones" completa-vs-numero via Identificacion.Parsear/
// ComputarStreamId/DirectorioColaborador.NormalizarNumeroDocumento, tokenizacion de "nombre" via
// DirectorioColaborador.TokenizarNombre, filtro AND, paginacion keyset) es responsabilidad de
// projection-implementer.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarDirectorioColaboradores")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "colaboradores/directorio")]
        HttpRequest req,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

// Issue #590: DTO de filtro tipado del body QUERY (MEF-ADR-0042 seccion 3) -- vive en el feature
// folder de esta query, no en ReadModels: contrato de REQUEST, no la vista (MEF-ADR-0041). Al
// menos uno de Identificaciones/Nombre es obligatorio (422 si ambos vienen ausentes/vacios); si
// ambos vienen, combinan en AND.
public sealed record FiltroListarDirectorioColaboradores(
    IReadOnlyList<string>? Identificaciones,
    string? Nombre,
    CursorDirectorio? Cursor,
    int Take = 50);

// Cursor keyset: los dos campos visibles de la ultima fila recibida (orden
// OrderBy(NombreCompleto).ThenBy(Id)). Cursor con un solo campo presente (el otro ausente/null) es
// 422 -- "cursor incompleto".
public sealed record CursorDirectorio(string NombreCompleto, string Identificacion);

// Issue #590: DTO de respuesta HTTP, excepcion bajo Rule of Three (MEF-ADR-0018,
// skills/projections/read-apis.md "El GET serializa la vista; el DTO de respuesta es excepcion")
// -- mismo proposito que FichaColaboradorRespuesta (ObtenerFichaColaborador): ocultar el centinela
// de vigencia abierta (DirectorioColaborador.CentinelaVigenciaAbierta), que es estructura INTERNA
// de filtrado/indexacion del read model y jamas debe salir por la API. No expone TokensNombre --
// estructura interna de busqueda, nunca contrato de respuesta.
public sealed record DirectorioColaboradorRespuesta(
    string Identificacion,
    string TipoDocumento,
    string NumeroDocumento,
    string NombreCompleto,
    string CodigoColaborador,
    string? CodigoSede,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta)
{
    public static DirectorioColaboradorRespuesta DesdeVista(DirectorioColaborador vista)
    {
        throw new NotImplementedException();
    }
}
