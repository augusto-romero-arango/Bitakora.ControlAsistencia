using System.Buffers.Text;
using System.Text;
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

// QUERY (RFC 10008, MEF-ADR-0042) de fichas vigentes. Comparte ruta con ObtenerFichaColaborador:
// el verbo QUERY distingue (MEF-ADR-0042 seccion 5).
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    // Take opcional, 1..200 o 422 (CA-ADR-0039); sin Take el llamador interno recibe todo el filtro
    // (CA-ADR-0038).
    private const int TakeMaximo = 200;

    // Ver el comentario en Run: por que MatchesSql y no LINQ, y por que nameof.
    private const string SqlContenimientoEtiquetas =
        $"(data->>'{nameof(FichaColaborador.EtiquetasNormalizadas)}')::jsonb @> ?::jsonb";

    [Function("ListarFichasColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "colaboradores/fichas")]
        HttpRequest req,
        CancellationToken ct)
    {
        // 415 ANTES de leer el body: ReadFromJsonAsync lanza si el Content-Type no es un tipo JSON
        // conocido, y esa excepcion NO es JsonException (se escaparia como 500 sin este guard).
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

        // El back jamas resuelve "hoy": lo resuelve quien consulta, en su zona horaria. STJ no lanza
        // por la ausencia (queda en 0001-01-01), asi que el 422 depende de esta validacion.
        if (filtro.FechaReferencia == default)
            return NoProcesable("FechaReferencia es obligatoria");

        if (filtro.Take is { } takeRecibido && (takeRecibido < 1 || takeRecibido > TakeMaximo))
            return NoProcesable($"Take debe estar entre 1 y {TakeMaximo}");

        // El cursor llega como JsonElement para que un no-string (la forma vieja, un objeto) sea 422 y
        // no un 400 de deserializacion.
        CursorFicha? cursor = null;
        if (filtro.Cursor is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } cursorCrudo)
        {
            if (cursorCrudo.ValueKind != JsonValueKind.String
                || !CursorFicha.TryDecodificar(cursorCrudo.GetString()!, out cursor))
                return NoProcesable("El cursor no es valido");
        }

        // Ninguna ficha lleva sede en blanco (AsignarSedeBodyValidator exige NotEmpty): es error del
        // cliente, no una consulta que deba responder 200 con lista vacia.
        if (filtro.CodigoSede is { } codigoSedeEnBlanco && string.IsNullOrWhiteSpace(codigoSedeEnBlanco))
            return NoProcesable("CodigoSede, si viene, no puede estar en blanco");

        // Ninguna ficha lleva Guid.Empty (AsignarJornadaBodyValidator exige NotEmpty): es error del
        // cliente, no una consulta que deba responder 200 con lista vacia.
        if (filtro.JornadaId == Guid.Empty)
            return NoProcesable("JornadaId, si viene, no puede ser Guid vacio");

        var etiquetasNormalizadas = NormalizarEtiquetas(filtro.Etiquetas);
        if (etiquetasNormalizadas is null)
            return NoProcesable("Una etiqueta del filtro es invalida (categoria o valor vacios)");

        // Siempre el tenant de ITenantContext, nunca uno que llegue por el body (BOLA/IDOR).
        await using var session = store.QuerySession(tenantContext.TenantId);

        // VigenteHasta es inclusivo: el dia efectivo de terminacion es el ULTIMO dia vigente. El
        // centinela de vinculacion abierta siempre pasa este filtro.
        IQueryable<FichaColaborador> query = session.Query<FichaColaborador>()
            .Where(f => f.VigenteHasta >= filtro.FechaReferencia);

        // AND por etiquetas en UNA operacion de containment JSONB. Verificado con Marten 9.12.0 +
        // Postgres 16 (EXPLAIN, enable_seqscan=off): el indexer LINQ sobre Dictionary traduce a ->>
        // por clave y nunca usa el GIN; Contains(KeyValuePair) genera @> contra un shape equivocado
        // ([{"Key":...}]) y devuelve 0 siempre. Solo MatchesSql con el MISMO shape del indice que
        // declara el worker (.Index(x => x.EtiquetasNormalizadas, gin)) da Bitmap Index Scan.
        // nameof y no literal: un rename que no lo alcanzara devolveria 0 resultados sin error.
        // MatchesSql no evade el filtro de tenant (verificado con ToCommand): entra en el mismo AND
        // que "d.tenant_id = :p0" y el JSON viaja parametrizado.
        if (etiquetasNormalizadas.Count > 0)
        {
            var etiquetasJson = JsonSerializer.Serialize(etiquetasNormalizadas);
            query = query.Where(f => f.MatchesSql(SqlContenimientoEtiquetas, etiquetasJson));
        }

        // Igualdad EXACTA (case-sensitive): el codigo se persiste sin normalizar, asi que el filtro
        // no puede normalizar lo que el otro lado no normalizo.
        if (filtro.CodigoSede is { } codigoSede)
            query = query.Where(f => f.CodigoSede == codigoSede);

        if (filtro.JornadaId is { } jornadaId)
            query = query.Where(f => f.JornadaId == jornadaId);

        // Keyset sobre (NombreCompleto, Id). CompareTo(...) > 0 traduce a SQL sobre campos string;
        // string.Compare(...) no, y Marten lanza BadLinqExpressionException (verificado en spike).
        if (cursor is { } c)
        {
            var cursorNombre = c.NombreCompleto;
            var cursorId = c.Id;
            query = query.Where(f =>
                f.NombreCompleto.CompareTo(cursorNombre) > 0
                || (f.NombreCompleto == cursorNombre && f.Id.CompareTo(cursorId) > 0));
        }

        var ordenada = query.OrderBy(f => f.NombreCompleto).ThenBy(f => f.Id);
        var fichas = filtro.Take is { } take
            ? await ordenada.Take(take + 1).ToListAsync(ct)
            : await ordenada.ToListAsync(ct);

        // Nunca 404: una pagina sin resultados es 200 con elementos vacios. Se piden Take + 1 filas:
        // la sobrante solo indica que hay siguienteCursor (CA-ADR-0039).
        return new OkObjectResult(Componer(fichas, filtro.Take));
    }

    internal static ListaFichasColaborador Componer(IReadOnlyList<FichaColaborador> filas, int? take)
    {
        if (take is not { } t || filas.Count <= t)
            return new ListaFichasColaborador(filas.Select(FichaColaboradorRespuesta.DesdeVista).ToList(), null);

        var pagina = filas.Take(t).ToList();
        var ultima = pagina[^1];
        return new ListaFichasColaborador(
            pagina.Select(FichaColaboradorRespuesta.DesdeVista).ToList(),
            CursorFicha.Codificar(ultima.NombreCompleto, ultima.Id));
    }

    // Normaliza con Etiqueta.Crear: un solo algoritmo de normalizacion, el del VO. null = algun par
    // inaceptable (422); vacio = sin filtro de etiquetas.
    private static Dictionary<string, string>? NormalizarEtiquetas(IReadOnlyList<FiltroEtiqueta>? pares)
    {
        var normalizadas = new Dictionary<string, string>();

        foreach (var par in pares ?? [])
        {
            // STJ acepta "etiquetas":[null] pese a la anotacion no-nullable: sin este guard seria una
            // NullReferenceException que sale como 500 en vez de 422.
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

            // Un valor por categoria: si el cliente la repite, la ultima gana.
            normalizadas[etiqueta.CategoriaNormalizada] = etiqueta.ValorNormalizado;
        }

        return normalizadas;
    }

    // MEF-ADR-0042 seccion 3: el 422 se emite con mensaje, nunca como codigo pelado.
    private static ObjectResult NoProcesable(string mensaje) =>
        new(mensaje) { StatusCode = StatusCodes.Status422UnprocessableEntity };
}

// FechaReferencia es OBLIGATORIA. CodigoSede filtra por la sede de la vinculacion vigente, solo
// por codigo: el nombre lo resuelve el consumidor contra FichaSede.
public sealed record FiltroListarFichasColaborador(
    DateOnly FechaReferencia,
    IReadOnlyList<FiltroEtiqueta>? Etiquetas,
    JsonElement? Cursor,
    int? Take = null,
    string? CodigoSede = null,
    Guid? JornadaId = null);

// Par categoria:valor SIN normalizar.
public sealed record FiltroEtiqueta(string Categoria, string Valor);

// Cursor opaco: las claves keyset (NombreCompleto, Id) de la ultima fila en JSON + base64url. Solo
// este endpoint lo interpreta; uno indescifrable o incompleto es 422.
public sealed record CursorFicha(string NombreCompleto, string Id)
{
    public static string Codificar(string nombreCompleto, string id) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new CursorFicha(nombreCompleto, id))));

    public static bool TryDecodificar(string cursor, out CursorFicha? resultado)
    {
        resultado = null;
        try
        {
            var decodificado = JsonSerializer.Deserialize<CursorFicha>(
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)));
            if (decodificado is not { NombreCompleto: not null, Id: not null })
                return false;
            resultado = decodificado;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }
}
