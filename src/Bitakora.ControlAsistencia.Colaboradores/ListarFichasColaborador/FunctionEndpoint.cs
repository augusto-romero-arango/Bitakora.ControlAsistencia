using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ListarFichasColaborador;

// Issue #373: listado QUERY (RFC 10008, MEF-ADR-0042) de fichas vigentes con filtro AND por
// etiquetas y paginacion keyset -- segunda mitad del desglose de #356 (ya en main: la vista
// FichaColaborador, la consulta puntual ObtenerFichaColaborador y su proyeccion). Este issue NO
// crea proyeccion ni read model nuevos -- consulta la MISMA vista materializada via (a')
// (session.Query<FichaColaborador>(), skills/projections/read-apis.md), sumando los indices del
// seam del worker (CA-5, ConfiguracionMartenProjectionsColaboradores).
//
// Mismo segmento de recurso que ObtenerFichaColaborador ("colaboradores/fichas") -- el verbo QUERY
// distingue, el nombre/ruta no cambian (MEF-ADR-0006 enmienda MEF-ADR-0042 seccion 5,
// skills/projections/naming.md).
//
// Stub de fase roja (projection-test-writer, MEF-ADR-0033): Run() lanza NotImplementedException a
// proposito -- 415/400/422, el filtro AND por etiquetas (Etiqueta.Crear, normalizacion simetrica),
// la paginacion keyset (OrderBy(NombreCompleto).ThenBy(Id)) y el clamp del Take son responsabilidad
// de projection-implementer. Los tests de FunctionEndpointTests.cs fallan contra este stub por
// diseno.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarFichasColaborador")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "colaboradores/fichas")]
        HttpRequest req,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

// Issue #373: DTO de filtro tipado del body QUERY (MEF-ADR-0042 seccion 3, contrato fijado por el
// issue) -- vive en el feature folder de esta query, no en ReadModels: es contrato de REQUEST, no
// la vista (MEF-ADR-0041 -- el DTO de filtro de MEF-ADR-0042 no reabre la excepcion del DTO de
// RESPUESTA). FechaReferencia es OBLIGATORIA -- el back jamas resuelve "hoy" (decision de
// refinamiento del issue): el "hoy" lo resuelve quien consulta, en su propia zona horaria.
public sealed record FiltroListarFichasColaborador(
    DateOnly FechaReferencia,
    IReadOnlyList<FiltroEtiqueta>? Etiquetas,
    CursorFicha? Cursor,
    int Take = 50);

// Par categoria:valor SIN normalizar -- el endpoint construye Etiqueta.Crear(Categoria, Valor) con
// cada par (Tell-don't-Ask, MEF-ADR-0012: un solo algoritmo de normalizacion, el del VO). Si
// Etiqueta.Crear rechaza el par (categoria/valor vacios), la respuesta es 422.
public sealed record FiltroEtiqueta(string Categoria, string Valor);

// Cursor keyset: los dos campos visibles de la ultima fila recibida (orden
// OrderBy(NombreCompleto).ThenBy(Id) -- decision de refinamiento del issue). Cursor con un solo
// campo presente (el otro ausente/null) es 422 -- "cursor incompleto".
public sealed record CursorFicha(string NombreCompleto, string Id);
