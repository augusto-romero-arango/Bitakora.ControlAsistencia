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
// ya materializa -- via (a'), session.Query<T>(). No crea proyeccion ni read model: mismo corte que
// ListarFichasColaborador hizo sobre la ficha.
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    // Tope de pagina (MEF-ADR-0042 seccion 2): el Take del cliente jamas llega crudo a Marten.
    private const int TakeMaximo = 200;

    // Containment JSONB del filtro por tokens: un array jsonb @> otro array jsonb es "el primero
    // contiene TODOS los elementos del segundo" -- la semantica exacta de "contiene todos los
    // tokens, en cualquier orden", en UNA operacion elegible para GIN.
    //
    // Verificado contra Postgres 16 real (EXPLAIN con enable_seqscan=off, revision de este issue):
    // esta expresion reproduce la del indice que declara el worker
    // (USING gin (((data ->> 'TokensNombre')::jsonb))) y el planner elige Bitmap Index Scan. Un
    // Where(d => d.TokensNombre.Contains(token)) por token traduce a comparaciones ->> y cae en Seq
    // Scan (mismo hallazgo que el spike de ListarFichasColaborador sobre EtiquetasNormalizadas).
    //
    // El nombre del campo se interpola con nameof, nunca como literal: es el unico punto donde una
    // propiedad de la vista viaja como texto, y un rename que no lo alcanzara dejaria el filtro
    // devolviendo 0 resultados sin error de compilacion ni de runtime.
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

        // Cursor con un solo campo presente -> 422. Un cursor con AMBOS campos ausentes cae en la
        // misma rama: quien no quiere paginar omite "cursor" por completo, no envia un objeto vacio.
        if (filtro.Take is { } takeRecibido && (takeRecibido < 1 || takeRecibido > TakeMaximo))
            return NoProcesable($"Take debe estar entre 1 y {TakeMaximo}");

        CursorDirectorio? cursor = null;
        if (filtro.Cursor is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } cursorCrudo)
        {
            if (cursorCrudo.ValueKind != JsonValueKind.String
                || !CursorDirectorio.TryDecodificar(cursorCrudo.GetString()!, out cursor))
                return NoProcesable("El cursor no es valido");
        }

        // MEF-ADR-0028: la sesion se abre SIEMPRE acotada al tenant que resuelve ITenantContext --
        // nunca a un tenant id que llegara por el body.
        await using var session = store.QuerySession(tenantContext.TenantId);

        IQueryable<DirectorioColaborador> query = session.Query<DirectorioColaborador>();

        // OR dentro del campo: cada valor se clasifica -- si Identificacion.Parsear lo acepta va por
        // igualdad de Id (la PK del documento, armada con ComputarStreamId: punto unico de
        // conversion, MEF-ADR-0037); si lo rechaza va por igualdad de NumeroDocumento normalizado
        // con la regla de la vista. Un valor que no corresponde a nadie no produce error: solo no
        // aporta filas. Cualquiera de las dos listas puede quedar vacia -- Marten genera igual un
        // `= ANY(...)` que no matchea nada (verificado contra Postgres 16).
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

        // Lista plana sin envoltura: el cliente deriva el cursor de la ultima fila y detecta el fin
        // por una pagina con menos de Take filas. Los colaboradores con vinculacion terminada SI
        // aparecen -- el directorio no filtra por vigencia.
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

// DTO de filtro tipado del body QUERY (MEF-ADR-0042 seccion 3): vive en el feature folder, no en
// ReadModels -- es contrato de REQUEST, no la vista (MEF-ADR-0041). Al menos uno de
// Identificaciones/Nombre es obligatorio; si vienen ambos, combinan en AND.
public sealed record FiltroListarDirectorioColaboradores(
    IReadOnlyList<string>? Identificaciones,
    string? Nombre,
    JsonElement? Cursor,
    int? Take = null);

// Cursor keyset: los dos campos visibles de la ultima fila recibida, en el orden
// OrderBy(NombreCompleto).ThenBy(Id).
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

// DTO de respuesta, excepcion bajo Rule of Three (MEF-ADR-0018, MEF-ADR-0041 decision 4) por el
// mismo motivo que FichaColaboradorRespuesta: oculta el centinela de vigencia abierta y TokensNombre,
// estructura interna de indexacion que jamas es contrato de respuesta.
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
