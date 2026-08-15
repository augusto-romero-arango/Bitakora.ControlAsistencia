using System.Text.Json;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.MultiTenancy;
using Marten;
using Marten.Linq.MatchesSql; // MatchesSql: unica forma verificada de containment JSONB elegible para GIN sobre Dictionary
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
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    // CA-3: tope de pagina (MEF-ADR-0042 seccion 2) -- el Take del cliente jamas llega crudo a
    // Marten.
    private const int TakeMaximo = 200;

    // Containment JSONB del filtro AND por etiquetas (CA-2). Ver el comentario en Run para por que
    // es MatchesSql y no LINQ, y por que el nombre del campo se interpola con nameof.
    private const string SqlContenimientoEtiquetas =
        $"(data->>'{nameof(FichaColaborador.EtiquetasNormalizadas)}')::jsonb @> ?::jsonb";

    [Function("ListarFichasColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "colaboradores/fichas")]
        HttpRequest req,
        CancellationToken ct)
    {
        // CA-4/RFC 10008 seccion 2.1: 415 ANTES de leer el body -- ReadFromJsonAsync lanza si el
        // Content-Type no es un tipo JSON conocido, y esa excepcion NO es JsonException (se
        // escaparia como 500 sin este guard).
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            {
                StatusCode = StatusCodes.Status415UnsupportedMediaType
            };

        FiltroListarFichasColaborador? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarFichasColaborador>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        // CA-4: FechaReferencia es OBLIGATORIA -- el back jamas resuelve "hoy" (decision de
        // refinamiento del issue). STJ no lanza por su ausencia (el campo queda en default,
        // 0001-01-01), asi que el 422 depende de esta validacion explicita.
        if (filtro.FechaReferencia == default)
            return NoProcesable("FechaReferencia es obligatoria");

        // CA-4: cursor keyset con un solo campo presente (el otro ausente/null) -> 422 "incompleto".
        // Un cursor con AMBOS campos ausentes cae en la misma rama -- un cliente que no quiere
        // paginar debe omitir "cursor" por completo (null), no enviar un objeto vacio.
        if (filtro.Cursor is { } cursorRecibido
            && (cursorRecibido.NombreCompleto is null || cursorRecibido.Id is null))
            return NoProcesable("El cursor debe traer NombreCompleto e Id, o ninguno de los dos");

        // CA-2: normalizacion simetrica -- Tell-don't-Ask (MEF-ADR-0012), ver NormalizarEtiquetas.
        var etiquetasNormalizadas = NormalizarEtiquetas(filtro.Etiquetas);
        if (etiquetasNormalizadas is null)
            return NoProcesable("Una etiqueta del filtro es invalida (categoria o valor vacios)");

        // CA-1/CA-2 (MEF-ADR-0028): la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por el body.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // CA-1: vigente a FechaReferencia = VigenteHasta >= FechaReferencia (el dia efectivo de
        // terminacion es el ULTIMO dia vigente, inclusive -- semantica verificada en el aggregate,
        // #349). El centinela de vinculacion abierta siempre satisface esta condicion.
        IQueryable<FichaColaborador> query = session.Query<FichaColaborador>()
            .Where(f => f.VigenteHasta >= filtro.FechaReferencia);

        // CA-2: filtro AND por etiquetas como UNA sola operacion de containment JSONB (precedente
        // #337: Marten traduce una igualdad de campo a containment @>, elegible para GIN) --
        // sin filtro de etiquetas (Etiquetas null/vacio) retorna todos los vigentes.
        //
        // Verificado por spike propio (Marten 9.12.0 + Postgres 16 real, EXPLAIN con
        // enable_seqscan=off): el indexer LINQ sobre Dictionary (f.EtiquetasNormalizadas[cat] ==
        // val, AND por cada par via multiples Where) SI traduce correctamente contra el dato real
        // -- pero como comparaciones ->> por clave, nunca como containment, y por eso NUNCA usa un
        // indice GIN (Seq Scan incluso forzando enable_seqscan=off). Contains(KeyValuePair) SI
        // genera containment (@>) pero contra un shape de JSON equivocado ([{"Key":...,"Value":...}])
        // que jamas calza con la representacion real de un Dictionary<string,string> serializado
        // por STJ ({"area":"tecnologia"}) -- devuelve 0 resultados siempre, sea que el par exista o
        // no. La forma verificada que SI usa el indice GIN que declara CA-5
        // (ConfiguracionMartenProjectionsColaboradores, .Index(x => x.EtiquetasNormalizadas, gin))
        // es MatchesSql reproduciendo el MISMO shape de expresion que Marten genera para ese
        // indice: (data->>'EtiquetasNormalizadas')::jsonb @> ?::jsonb (confirmado con EXPLAIN:
        // Bitmap Index Scan sobre el indice GIN, Recheck Cond identico).
        //
        // El nombre del campo se interpola con nameof, nunca como literal suelto: este SQL crudo es
        // el UNICO punto del sistema donde el nombre de una propiedad de la vista viaja como texto,
        // y un rename del read model que no lo alcanzara dejaria el filtro devolviendo 0 resultados
        // siempre, sin error de compilacion ni de runtime (mismo modo de falla silencioso que el
        // spike encontro en Contains(KeyValuePair)).
        //
        // MatchesSql NO evade el filtro de tenant (verificado en la revision inspeccionando el SQL
        // que Marten genera para esta query, via ToCommand(FetchType.FetchMany), sin Postgres): el
        // fragmento entra como un where mas del mismo AND que ya lleva "d.tenant_id = :p0", y el
        // JSON viaja parametrizado (":p4::jsonb"), nunca concatenado. MEF-ADR-0028 se sostiene.
        if (etiquetasNormalizadas.Count > 0)
        {
            var etiquetasJson = JsonSerializer.Serialize(etiquetasNormalizadas);
            query = query.Where(f => f.MatchesSql(SqlContenimientoEtiquetas, etiquetasJson));
        }

        // CA-3: paginacion keyset -- orden OrderBy(NombreCompleto).ThenBy(Id), predicado compuesto
        // "nombre > cursor.NombreCompleto OR (nombre == cursor.NombreCompleto AND id >
        // cursor.Id)". Verificado por spike propio: CompareTo(...) > 0 SI traduce a SQL sobre
        // campos string (d.data ->> 'NombreCompleto' > :p0 / d.id > :p0 para el Id, que es columna
        // dedicada del documento) -- a diferencia de string.Compare(...), que Marten no puede
        // reducir y lanza BadLinqExpressionException. Cierra el NO VERIFICADO de
        // skills/projections/read-apis.md para este dominio.
        if (filtro.Cursor is { NombreCompleto: { } cursorNombre, Id: { } cursorId })
        {
            query = query.Where(f =>
                f.NombreCompleto.CompareTo(cursorNombre) > 0
                || (f.NombreCompleto == cursorNombre && f.Id.CompareTo(cursorId) > 0));
        }

        // CA-3: Take se acota en el servidor -- nunca se pasa crudo a Marten.
        var take = Math.Clamp(filtro.Take, 1, TakeMaximo);

        var fichas = await query
            .OrderBy(f => f.NombreCompleto).ThenBy(f => f.Id)
            .Take(take)
            .ToListAsync(ct);

        // CA-4: VigenteHasta vacio en la respuesta de vinculacion abierta -- el centinela jamas
        // sale por la API (misma regla que #356 CA-6). Reutiliza FichaColaboradorRespuesta.DesdeVista
        // de ObtenerFichaColaborador en vez de duplicar la misma traduccion (MEF-ADR-0018): es el
        // mismo DTO de respuesta -- ya excepcion bajo MEF-ADR-0041 decision 4 -- para el mismo read
        // model, ahora con un segundo consumidor.
        //
        // CA-4: nunca 404 -- una pagina sin resultados es 200 con lista vacia. Sin envelope
        // (propuesta del issue, MEF-ADR-0018): el cliente deriva el cursor de NombreCompleto/Id de
        // la ultima fila; fin de la lista = pagina con menos de Take filas.
        return new OkObjectResult(fichas.Select(FichaColaboradorRespuesta.DesdeVista).ToList());
    }

    // CA-2: normalizacion simetrica -- Tell-don't-Ask (MEF-ADR-0012). Construye
    // Etiqueta.Crear(Categoria, Valor) con cada par recibido: es el VO quien decide como se
    // normaliza (un solo algoritmo de normalizacion en el sistema), nunca el endpoint
    // reimplementandolo. Devuelve null cuando algun par es inaceptable -- el llamador lo traduce a
    // 422. Etiquetas null/vacio produce un diccionario vacio: sin filtro de etiquetas.
    private static Dictionary<string, string>? NormalizarEtiquetas(IReadOnlyList<FiltroEtiqueta>? pares)
    {
        var normalizadas = new Dictionary<string, string>();

        foreach (var par in pares ?? [])
        {
            // El body es entrada del cliente: STJ acepta un elemento null dentro del array
            // ("etiquetas":[null]) pese a la anotacion no-nullable del record. Sin este guard, el
            // acceso a par.Categoria seria una NullReferenceException que escapa del catch de
            // ArgumentException y sale como 500 donde el RFC 10008 pide 422.
            if (par is null)
                return null;

            Etiqueta etiqueta;
            try
            {
                etiqueta = Etiqueta.Crear(par.Categoria, par.Valor);
            }
            catch (ArgumentException)
            {
                return null;
            }

            // Un valor por categoria (misma invariante que el aggregate/la proyeccion, #355): si el
            // cliente repite la misma categoria dos veces, la ultima gana.
            normalizadas[etiqueta.CategoriaNormalizada] = etiqueta.ValorNormalizado;
        }

        return normalizadas;
    }

    // RFC 10008 seccion 2.1 / MEF-ADR-0042 seccion 3: el 422 se emite como ObjectResult con mensaje,
    // nunca como codigo pelado -- misma forma que el 400 del parseo del id de ruta (MEF-ADR-0037).
    private static ObjectResult NoProcesable(string mensaje) =>
        new(mensaje) { StatusCode = StatusCodes.Status422UnprocessableEntity };
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
