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

// Issue #590: QUERY colaboradores/directorio (MEF-ADR-0042) sobre la vista DirectorioColaborador
// que #587 ya materializa -- via (a') (session.Query<DirectorioColaborador>()), segunda mitad del
// desglose de #587. Mismo patron que ListarFichasColaborador (#373): NO crea proyeccion ni read
// model nuevos, solo consulta la vista y suma el par de config-tests de compatibilidad de columna
// de version (CA-5, MEF-ADR-0034 seccion 6).
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    // CA-1: tope de pagina (MEF-ADR-0042 seccion 2) -- el Take del cliente jamas llega crudo a
    // Marten.
    private const int TakeMaximo = 200;

    // Containment JSONB del filtro por tokens de nombre (CA-3): mismo shape de expresion que el GIN
    // que #587 declara sobre TokensNombre (ConfiguracionMartenProjectionsColaboradores), mismo
    // patron verificado que SqlContenimientoEtiquetas en ListarFichasColaborador (#373) -- un array
    // jsonb @> otro array jsonb es "el primero contiene TODOS los elementos del segundo",
    // exactamente la semantica de "contiene todos los tokens", elegible para Bitmap Index Scan.
    // nameof obligatorio: un rename de la vista no debe dejar el filtro devolviendo 0 en silencio.
    private const string SqlContenimientoTokens =
        $"(data->>'{nameof(DirectorioColaborador.TokensNombre)}')::jsonb @> ?::jsonb";

    [Function("ListarDirectorioColaboradores")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "colaboradores/directorio")]
        HttpRequest req,
        CancellationToken ct)
    {
        // 415 ANTES de leer el body -- ReadFromJsonAsync lanza si el Content-Type no es un tipo
        // JSON conocido, y esa excepcion NO es JsonException (se escaparia como 500 sin este guard).
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

        // CA-1: Nombre presente pero en blanco es 422 -- un cliente que no quiere filtrar por
        // nombre debe omitir el campo por completo (null), no enviar un texto vacio.
        if (filtro.Nombre is not null && string.IsNullOrWhiteSpace(filtro.Nombre))
            return NoProcesable("Nombre, si viene, no puede estar en blanco");

        // CA-1: reglas de Identificaciones cuando el campo viene presente (no null) -- vacia, con
        // mas de 200 valores, o con algun valor null/en blanco (STJ acepta un elemento null dentro
        // del array pese a la anotacion IReadOnlyList<string>, mismo gotcha que FiltroEtiqueta en el
        // hermano ListarFichasColaborador).
        if (filtro.Identificaciones is { } identificacionesRecibidas)
        {
            if (identificacionesRecibidas.Count == 0)
                return NoProcesable("Identificaciones, si viene, no puede estar vacia");

            if (identificacionesRecibidas.Count > 200)
                return NoProcesable("Identificaciones admite maximo 200 valores");

            if (identificacionesRecibidas.Any(string.IsNullOrWhiteSpace))
                return NoProcesable("Identificaciones no puede traer valores nulos ni en blanco");
        }

        // CA-1: al menos uno de los dos campos de filtro es obligatorio.
        var tieneIdentificaciones = filtro.Identificaciones is { Count: > 0 };
        var tieneNombre = !string.IsNullOrWhiteSpace(filtro.Nombre);
        if (!tieneIdentificaciones && !tieneNombre)
            return NoProcesable("Debe indicar identificaciones o nombre");

        // CA-1: cursor keyset con un solo campo presente (el otro ausente/null) -> 422
        // "incompleto". Un cursor con AMBOS campos ausentes cae en la misma rama -- un cliente que
        // no quiere paginar debe omitir "cursor" por completo (null), no enviar un objeto vacio.
        if (filtro.Cursor is { } cursorRecibido
            && (cursorRecibido.NombreCompleto is null || cursorRecibido.Identificacion is null))
            return NoProcesable("El cursor debe traer NombreCompleto e Identificacion, o ninguno de los dos");

        // CA-2/MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por el body.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        IQueryable<DirectorioColaborador> query = session.Query<DirectorioColaborador>();

        // CA-2: OR dentro del campo Identificaciones -- cada valor se clasifica: si
        // Identificacion.Parsear lo acepta (contiene "-", tipo en la lista cerrada PILA, numero no
        // vacio) va por igualdad de Id (la PK del documento, via ComputarStreamId -- punto unico de
        // conversion, MEF-ADR-0037); si lo rechaza, va por igualdad de NumeroDocumento (normalizado
        // con la MISMA regla que la vista, Tell-don't-Ask MEF-ADR-0012). Un valor que no corresponde
        // a nadie no produce error -- solo no aporta filas.
        if (tieneIdentificaciones)
        {
            var ids = new List<string>();
            var numeros = new List<string>();

            foreach (var valor in filtro.Identificaciones!)
            {
                try
                {
                    var identificacion = Identificacion.Parsear(valor);
                    ids.Add(ColaboradorAggregateRoot.ComputarStreamId(identificacion));
                }
                catch (ArgumentException)
                {
                    numeros.Add(DirectorioColaborador.NormalizarNumeroDocumento(valor));
                }
            }

            query = query.Where(d => ids.Contains(d.Id) || numeros.Contains(d.NumeroDocumento));
        }

        // CA-3: filtro AND por tokens de nombre, tokenizado con la MISMA normalizacion de la vista
        // (DirectorioColaborador.TokenizarNombre) -- exige contencion de TODOS los tokens, en
        // cualquier orden, token completo (no prefijo).
        if (tieneNombre)
        {
            var tokens = DirectorioColaborador.TokenizarNombre(filtro.Nombre!);
            var tokensJson = JsonSerializer.Serialize(tokens);
            query = query.Where(d => d.MatchesSql(SqlContenimientoTokens, tokensJson));
        }

        // Paginacion keyset -- orden OrderBy(NombreCompleto).ThenBy(Id), predicado compuesto
        // "nombre > cursor.NombreCompleto OR (nombre == cursor.NombreCompleto AND id >
        // cursor.Identificacion)". CompareTo(...) traduce a SQL sobre campos string (verificado en
        // #373 para el mismo shape de predicado); string.Compare no.
        if (filtro.Cursor is { NombreCompleto: { } cursorNombre, Identificacion: { } cursorId })
        {
            query = query.Where(d =>
                d.NombreCompleto.CompareTo(cursorNombre) > 0
                || (d.NombreCompleto == cursorNombre && d.Id.CompareTo(cursorId) > 0));
        }

        // CA-1: Take se acota en el servidor -- nunca se pasa crudo a Marten.
        var take = Math.Clamp(filtro.Take, 1, TakeMaximo);

        var directorio = await query
            .OrderBy(d => d.NombreCompleto).ThenBy(d => d.Id)
            .Take(take)
            .ToListAsync(ct);

        // CA-4: lista plana sin envoltura -- el cliente deriva el cursor de la ultima fila, fin de
        // lista = pagina con menos de Take filas. Los colaboradores con vinculacion terminada SI
        // aparecen: el directorio no filtra por vigencia.
        return new OkObjectResult(directorio.Select(DirectorioColaboradorRespuesta.DesdeVista).ToList());
    }

    // RFC 10008 seccion 2.1 / MEF-ADR-0042 seccion 3: el 422 se emite como ObjectResult con mensaje,
    // nunca como codigo pelado.
    private static ObjectResult NoProcesable(string mensaje) =>
        new(mensaje) { StatusCode = StatusCodes.Status422UnprocessableEntity };
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
    // El centinela se lee de la propia vista (DirectorioColaborador.CentinelaVigenciaAbierta),
    // nunca de un literal repetido aqui -- mismo criterio que FichaColaboradorRespuesta.DesdeVista.
    public static DirectorioColaboradorRespuesta DesdeVista(DirectorioColaborador vista) =>
        new(
            vista.Id,
            vista.TipoDocumento,
            vista.NumeroDocumento,
            vista.NombreCompleto,
            vista.CodigoColaborador,
            vista.CodigoSede,
            vista.VigenteDesde,
            vista.VigenteHasta == DirectorioColaborador.CentinelaVigenciaAbierta ? null : vista.VigenteHasta);
}
