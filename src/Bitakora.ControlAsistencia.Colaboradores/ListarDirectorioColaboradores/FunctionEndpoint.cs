using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.MultiTenancy;
using Marten;
using Marten.Linq.MatchesSql; // MatchesSql: unica forma verificada de containment JSONB elegible para GIN
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ListarDirectorioColaboradores;

// QUERY colaboradores/directorio (MEF-ADR-0042) sobre la vista DirectorioColaborador que el worker
// ya materializa.
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    // Take opcional, 1..200 o 422 tambien con identificaciones (CA-ADR-0039 decision 9).
    private const int TakeMaximo = 200;

    // jsonb @> jsonb = "contiene TODOS los tokens, en cualquier orden", en una operacion elegible
    // para GIN. La expresion debe reproducir la del indice que declara el worker
    // (USING gin (((data ->> 'TokensNombre')::jsonb))): verificado con EXPLAIN en Postgres 16 que
    // usa Bitmap Index Scan; un Where(...Contains(token)) por token cae en Seq Scan.
    // nameof y no literal: un rename que no lo alcanzara devolveria 0 resultados sin error.
    private const string SqlContenimientoTokens =
        $"(data->>'{nameof(DirectorioColaborador.TokensNombre)}')::jsonb @> ?::jsonb";

    [Function("ListarDirectorioColaboradores")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "colaboradores/directorio")]
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

        FiltroListarDirectorioColaboradores? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarDirectorioColaboradores>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        // STJ acepta un elemento null dentro del array pese a la anotacion IReadOnlyList<string>:
        // sin este guard, un valor nulo llegaria a Identificacion.Parsear como 500.
        if (filtro.Identificaciones is { } identificaciones)
        {
            if (identificaciones.Count == 0)
                return NoProcesable("Identificaciones, si viene, no puede estar vacia");

            if (identificaciones.Any(string.IsNullOrWhiteSpace))
                return NoProcesable("Identificaciones no puede traer valores nulos ni en blanco");
        }

        // Tokenizar ANTES de abrir la sesion, con la MISMA normalizacion de la vista
        // (Tell-don't-Ask, MEF-ADR-0012). Un nombre que no produce ningun token -- en blanco, o solo
        // puntuacion -- es 422 y nunca llega a la query: '[]' esta contenido en CUALQUIER array
        // jsonb (verificado contra Postgres 16), asi que un filtro de cero tokens devolveria el
        // directorio completo justo cuando el cliente pidio buscar por nombre.
        IReadOnlyList<string> tokensNombre = [];
        if (filtro.Nombre is { } nombre)
        {
            tokensNombre = DirectorioColaborador.TokenizarNombre(nombre);
            if (tokensNombre.Count == 0)
                return NoProcesable("Nombre, si viene, debe traer al menos una letra o digito");
        }

        var tieneIdentificaciones = filtro.Identificaciones is { Count: > 0 };
        if (!tieneIdentificaciones && tokensNombre.Count == 0)
            return NoProcesable("Debe indicar identificaciones o nombre");

        if (filtro.Take is { } takeRecibido && (takeRecibido < 1 || takeRecibido > TakeMaximo))
            return NoProcesable($"Take debe estar entre 1 y {TakeMaximo}");

        // El cursor llega como JsonElement para que un no-string (la forma vieja, un objeto) sea 422 y
        // no un 400 de deserializacion.
        CursorDirectorio? cursor = null;
        if (filtro.Cursor is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } cursorCrudo)
        {
            if (cursorCrudo.ValueKind != JsonValueKind.String
                || !CursorDirectorio.TryDecodificar(cursorCrudo.GetString()!, out cursor))
                return NoProcesable("El cursor no es valido");
        }

        // Siempre el tenant de ITenantContext, nunca uno que llegue por el body (BOLA/IDOR).
        await using var session = store.QuerySession(tenantContext.TenantId);

        IQueryable<DirectorioColaborador> query = session.Query<DirectorioColaborador>();

        // Cada valor va por Id si Identificacion.Parsear lo acepta, si no por NumeroDocumento
        // normalizado; un valor sin dueno no es error, solo no aporta filas. Una lista vacia genera
        // un `= ANY(...)` que no matchea nada (verificado contra Postgres 16).
        if (tieneIdentificaciones)
        {
            var ids = new List<string>();
            var numeros = new List<string>();

            foreach (var valor in filtro.Identificaciones!)
            {
                try
                {
                    ids.Add(ColaboradorAggregateRoot.ComputarStreamId(Identificacion.Parsear(valor)));
                }
                catch (ArgumentException)
                {
                    numeros.Add(DirectorioColaborador.NormalizarNumeroDocumento(valor));
                }
            }

            query = query.Where(d => ids.Contains(d.Id) || numeros.Contains(d.NumeroDocumento));
        }

        if (tokensNombre.Count > 0)
        {
            var tokensJson = JsonSerializer.Serialize(tokensNombre);
            query = query.Where(d => d.MatchesSql(SqlContenimientoTokens, tokensJson));
        }

        // Paginacion keyset: "nombre > cursor.NombreCompleto OR (nombre == cursor.NombreCompleto AND
        // id > cursor.Identificacion)". CompareTo(...) traduce a SQL sobre campos string; string.Compare
        // no (verificado en el spike de ListarFichasColaborador para este mismo shape de predicado).
        if (cursor is { } c)
        {
            var cursorNombre = c.NombreCompleto;
            var cursorId = c.Identificacion;
            query = query.Where(d =>
                d.NombreCompleto.CompareTo(cursorNombre) > 0
                || (d.NombreCompleto == cursorNombre && d.Id.CompareTo(cursorId) > 0));
        }

        var ordenada = query.OrderBy(d => d.NombreCompleto).ThenBy(d => d.Id);
        var directorio = filtro.Take is { } take
            ? await ordenada.Take(take + 1).ToListAsync(ct)
            : await ordenada.ToListAsync(ct);

        // Take + 1 filas: la sobrante solo indica que hay siguienteCursor (CA-ADR-0039). Los
        // colaboradores con vinculacion terminada SI aparecen -- el directorio no filtra por vigencia.
        return new OkObjectResult(Componer(directorio, filtro.Take));
    }

    internal static ListaDirectorioColaboradores Componer(IReadOnlyList<DirectorioColaborador> filas, int? take)
    {
        if (take is not { } t || filas.Count <= t)
            return new ListaDirectorioColaboradores(
                filas.Select(DirectorioColaboradorRespuesta.DesdeVista).ToList(), null);

        var pagina = filas.Take(t).ToList();
        var ultima = pagina[^1];
        return new ListaDirectorioColaboradores(
            pagina.Select(DirectorioColaboradorRespuesta.DesdeVista).ToList(),
            CursorDirectorio.Codificar(ultima.NombreCompleto, ultima.Id));
    }

    // MEF-ADR-0042 seccion 3: el 422 se emite con mensaje, nunca como codigo pelado.
    private static ObjectResult NoProcesable(string mensaje) =>
        new(mensaje) { StatusCode = StatusCodes.Status422UnprocessableEntity };
}

// Al menos uno de Identificaciones/Nombre es obligatorio; si vienen ambos, combinan en AND.
public sealed record FiltroListarDirectorioColaboradores(
    IReadOnlyList<string>? Identificaciones,
    string? Nombre,
    JsonElement? Cursor,
    int? Take = null);

// Cursor opaco: las claves keyset (NombreCompleto, Id) de la ultima fila en JSON + base64url. Solo
// este endpoint lo interpreta; uno indescifrable o incompleto es 422.
public sealed record CursorDirectorio(string NombreCompleto, string Identificacion)
{
    public static string Codificar(string nombreCompleto, string identificacion) =>
        Base64Url.EncodeToString(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new CursorDirectorio(nombreCompleto, identificacion))));

    public static bool TryDecodificar(string cursor, out CursorDirectorio? resultado)
    {
        resultado = null;
        try
        {
            var decodificado = JsonSerializer.Deserialize<CursorDirectorio>(
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)));
            if (decodificado is not { NombreCompleto: not null, Identificacion: not null })
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

// DTO de excepcion (MEF-ADR-0041 decision 4): oculta el centinela de vigencia abierta y
// TokensNombre, estructura interna de indexacion que jamas es contrato de respuesta.
public sealed record DirectorioColaboradorRespuesta(
    string Identificacion,
    string TipoDocumento,
    string NumeroDocumento,
    string NombreCompleto,
    string CodigoColaborador,
    string? CodigoSede,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    Guid? JornadaId = null)
{
    // El centinela se lee de la propia vista, nunca de un literal repetido aqui.
    public static DirectorioColaboradorRespuesta DesdeVista(DirectorioColaborador vista) =>
        new(
            vista.Id,
            vista.TipoDocumento,
            vista.NumeroDocumento,
            vista.NombreCompleto,
            vista.CodigoColaborador,
            vista.CodigoSede,
            vista.VigenteDesde,
            vista.VigenteHasta == DirectorioColaborador.CentinelaVigenciaAbierta ? null : vista.VigenteHasta,
            vista.JornadaId);
}
