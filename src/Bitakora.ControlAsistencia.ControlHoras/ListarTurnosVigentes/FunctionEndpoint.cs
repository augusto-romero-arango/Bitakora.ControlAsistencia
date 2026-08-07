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
// Ruta sin {empleadoId}/{fecha}: reutiliza el mismo segmento de recurso
// "control-horas/turnos-vigentes" que ObtenerTurnoVigente (#328) -- naming.md: "una query reutiliza
// el mismo segmento de recurso que ya usa el comando/query de ese recurso". Sin colision de
// plantilla: la de ObtenerTurnoVigente lleva dos segmentos adicionales ({empleadoId}/{fecha}).
//
// CA-1/CA-2: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
// nunca a un tenant id que llegue por query string (MEF-ADR-0028/skills/projections/read-apis.md,
// mitigacion estructural contra BOLA/IDOR). desde/hasta/empleadoId SI vienen del query string: son
// el filtro del recurso, no el tenant.
//
// Contrato de la consulta: desde/hasta obligatorios con formato yyyy-MM-dd (CA-4), rango invertido
// rechazado con 400, empleadoId opcional filtra a un solo empleado (CA-2), recorte de rango con
// RangoConsulta.Recortar (CA-3) y 200 con lista vacia cuando no hay datos en el rango (CA-4)
// -- nunca 404: un rango sin turnos asignados no es un error.
//
// Issue #337 (CA-2/CA-3/CA-4): sedeId es un tercer filtro opcional, combinable con empleadoId y el
// rango -- "dias donde AL MENOS un bloque rige en esa sede" (issue #337, "Contexto"), por eso el
// predicado es Bloques.Any(b => b.SedeId == sedeId) y no un campo a nivel de TurnoVigente (la sede
// va por bloque, nunca por dia). Marten traduce Any() sobre la coleccion anidada a una consulta
// JSONB (MEF-ADR-0035). Los bloques con SedeId null (CA-4/CA-5, franjas sin sede o documentos
// proyectados antes de #336/#337) nunca igualan un sedeId no nulo, asi que quedan fuera de
// cualquier filtro por sede sin necesitar una rama explicita.
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

        // CA-2: empleadoId es opcional -- ausente = panorama de todos los empleados (consulta del
        // Programador); presente = filtrado a un solo empleado (consulta del Trabajador).
        // StringValues.ToString() devuelve cadena vacia cuando el parametro no viene, asi que
        // ausente y vacio se tratan igual -- sin filtro por empleado.
        var empleadoId = req.Query["empleadoId"].ToString();

        // Issue #337, CA-2/CA-3: sedeId es opcional -- ausente = sin filtro por sede (regresion de
        // #329 intacta, CA-3). Mismo tratamiento de StringValues.ToString() que empleadoId: ausente
        // y vacio se comportan igual.
        var sedeId = req.Query["sedeId"].ToString();

        // CA-3: recorte de la cota de 31 dias, siempre hacia adelante desde `desde`.
        var rangoAplicado = RangoConsulta.Recortar(desde, hasta);

        // CA-1/CA-2: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por query string (mitigacion
        // estructural contra BOLA/IDOR, MEF-ADR-0028/skills/projections/read-apis.md).
        // empleadoId/desde/hasta SI vienen del query string: son el filtro del recurso, no el
        // tenant.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // Composicion en pasos (no un unico Where con el filtro de empleadoId embebido
        // condicionalmente): mas legible que anidar el ternario dentro de la expresion LINQ.
        IQueryable<TurnoVigente> query = session.Query<TurnoVigente>()
            .Where(v => v.Fecha >= desde && v.Fecha <= rangoAplicado.HastaAplicado);

        if (!string.IsNullOrEmpty(empleadoId))
            query = query.Where(v => v.EmpleadoId == empleadoId);

        // CA-2 (issue #337): "dias donde al menos un bloque rige en esa sede" -- un dia multi-sede
        // (turno partido Suba/Chapinero) aparece bajo cualquiera de las sedes de sus bloques.
        if (!string.IsNullOrEmpty(sedeId))
            query = query.Where(v => v.Bloques.Any(b => b.SedeId == sedeId));

        // Orden sugerido por la investigacion del planner: por EmpleadoId y luego por Fecha --
        // estable para pintar grillas multi-empleado x rango de fechas.
        var turnos = await query
            .OrderBy(v => v.EmpleadoId)
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
