using System.Globalization;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

// Issue #329: tercera Function GET del BC sobre la vista materializada TurnoVigente (#328) --
// ninguna proyeccion nueva, ningun cambio al seam del worker (issue #329, "Necesidad de lectura":
// "Este issue NO crea read model, ni clase de proyeccion, ni lifecycle: solo la Function GET de
// listado"). Feature folder propio (un namespace por query, skills/projections/naming.md y
// read-apis.md): esta clase FunctionEndpoint no colisiona con las demas homonimas del ensamblado
// (ObtenerTurnoVigente/RegistrarMarcacionFunction/...) porque cada una vive en su propio
// namespace.
//
// Ruta sin {codigoColaborador}/{fecha}: reutiliza el mismo segmento de recurso
// "control-horas/turnos-vigentes" que ObtenerTurnoVigente (#328) -- naming.md: "una query reutiliza
// el mismo segmento de recurso que ya usa el comando/query de ese recurso". Sin colision de
// plantilla: la de ObtenerTurnoVigente lleva dos segmentos adicionales ({codigoColaborador}/{fecha}).
//
// CA-1/CA-2: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
// nunca a un tenant id que llegue por query string (MEF-ADR-0028/skills/projections/read-apis.md,
// mitigacion estructural contra BOLA/IDOR). desde/hasta/codigoColaborador SI vienen del query string: son
// el filtro del recurso, no el tenant.
//
// Contrato de la consulta: desde/hasta obligatorios con formato yyyy-MM-dd (CA-4), rango invertido
// rechazado con 400, codigoColaborador opcional filtra a un solo colaborador (CA-2), recorte de rango con
// RangoConsulta.Recortar (CA-3) y 200 con lista vacia cuando no hay datos en el rango (CA-4)
// -- nunca 404: un rango sin turnos asignados no es un error.
//
// Issue #337 (CA-2/CA-3/CA-4): sedeId es un tercer filtro opcional, combinable con codigoColaborador y el
// rango -- "dias donde AL MENOS un bloque rige en esa sede" (issue #337, "Contexto"), por eso el
// predicado es Bloques.Any(b => b.SedeId == sedeId) y no un campo a nivel de TurnoVigente (la sede
// va por bloque, nunca por dia). Sigue siendo la via de consulta (a') de MEF-ADR-0035 (LINQ sobre
// session.Query<TView>()): Marten soporta Any() dentro de colecciones hijas y traduce una igualdad
// como esta a containment JSONB -- data -> 'Bloques' @> '[{"SedeId": ...}]' --, la unica forma
// elegible para indice GIN (martendb.io/documents/querying/linq/child-collections.html). Esa misma
// semantica de containment resuelve CA-4/CA-5 sin rama explicita: un bloque sin la clave SedeId
// (franja sin sede, o documento proyectado antes de #336/#337) nunca contiene un sedeId no nulo.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    private const string FormatoFecha = "yyyy-MM-dd";

    [Function("ListarTurnosVigentes")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/turnos-vigentes")]
        HttpRequest req,
        CancellationToken ct)
    {
        // CA-4: desde/hasta son obligatorios -- ausencia o formato invalido devuelve 400. DateOnly
        // no liga directo desde el query string en el modelo aislado de Functions, se parsea con
        // formato explicito yyyy-MM-dd.
        if (!TryLeerFecha(req, "desde", out var desde, out var errorDesde))
            return new BadRequestObjectResult(errorDesde);

        if (!TryLeerFecha(req, "hasta", out var hasta, out var errorHasta))
            return new BadRequestObjectResult(errorHasta);

        // CA-4: rango invertido (hasta < desde) -> 400. Es un error del llamador, no una lista
        // vacia.
        if (hasta < desde)
            return new BadRequestObjectResult("El parametro 'hasta' no puede ser anterior a 'desde'");

        // CA-2: codigoColaborador es opcional -- ausente = panorama de todos los colaboradores (consulta del
        // Programador); presente = filtrado a un solo colaborador (consulta del Trabajador).
        // StringValues.ToString() devuelve cadena vacia cuando el parametro no viene, asi que
        // ausente y vacio se tratan igual -- sin filtro por colaborador.
        var codigoColaborador = req.Query["codigoColaborador"].ToString();

        // Issue #337, CA-2/CA-3: sedeId es opcional -- ausente = sin filtro por sede (regresion de
        // #329 intacta, CA-3). Mismo tratamiento de StringValues.ToString() que codigoColaborador: ausente
        // y vacio se comportan igual.
        var sedeId = req.Query["sedeId"].ToString();

        // CA-3: recorte de la cota de 31 dias, siempre hacia adelante desde `desde`.
        var rangoAplicado = RangoConsulta.Recortar(desde, hasta);

        // CA-1/CA-2: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por query string (mitigacion
        // estructural contra BOLA/IDOR, MEF-ADR-0028/skills/projections/read-apis.md).
        // codigoColaborador/desde/hasta SI vienen del query string: son el filtro del recurso, no el
        // tenant.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // Composicion en pasos (no un unico Where con el filtro de codigoColaborador embebido
        // condicionalmente): mas legible que anidar el ternario dentro de la expresion LINQ.
        IQueryable<TurnoVigente> query = session.Query<TurnoVigente>()
            .Where(v => v.Fecha >= desde && v.Fecha <= rangoAplicado.HastaAplicado);

        if (!string.IsNullOrEmpty(codigoColaborador))
            query = query.Where(v => v.CodigoColaborador == codigoColaborador);

        // CA-2 (issue #337): "dias donde al menos un bloque rige en esa sede" -- un dia multi-sede
        // (turno partido Suba/Chapinero) aparece bajo cualquiera de las sedes de sus bloques.
        if (!string.IsNullOrEmpty(sedeId))
            query = query.Where(v => v.Bloques.Any(b => b.SedeId == sedeId));

        // Orden sugerido por la investigacion del planner: por CodigoColaborador y luego por Fecha --
        // estable para pintar grillas multi-colaborador x rango de fechas.
        var turnos = await query
            .OrderBy(v => v.CodigoColaborador)
            .ThenBy(v => v.Fecha)
            .ToListAsync(ct);

        var respuesta = new ListaTurnosVigentes(
            desde, rangoAplicado.HastaAplicado, rangoAplicado.RangoRecortado, turnos);

        // CA-4: nunca 404 -- un rango sin turnos vigentes es 200 con Turnos: [].
        return new OkObjectResult(respuesta);
    }

    // Ausente y mal formado comparten respuesta y mensaje a proposito: StringValues.ToString()
    // devuelve cadena vacia cuando el parametro no viene, y TryParseExact la rechaza igual que a
    // "31-12-2026". Un solo camino de fallo, un solo 400 (CA-4).
    private static bool TryLeerFecha(
        HttpRequest req, string nombreParametro, out DateOnly fecha, out string? error)
    {
        if (DateOnly.TryParseExact(
                req.Query[nombreParametro].ToString(),
                FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
        {
            error = null;
            return true;
        }

        error = $"El parametro '{nombreParametro}' es obligatorio y debe tener el formato {FormatoFecha}";
        return false;
    }
}
