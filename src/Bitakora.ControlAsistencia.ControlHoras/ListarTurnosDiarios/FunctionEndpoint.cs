using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoDiario;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosDiarios;

// Issue #290: segunda Function GET del BC (skills/projections/naming.md: Listar{X}s por
// filtro/lista), sobre la MISMA vista materializada TurnoDiarioView que ya usa ObtenerTurnoDiario
// (#289) -- ninguna proyeccion nueva, ningun cambio al seam del worker. Feature folder propio (un
// namespace por query, skills/projections/read-apis.md y naming.md): esta clase FunctionEndpoint
// no colisiona con las demas homonimas del ensamblado porque cada una vive en su propio namespace.
//
// Ruta sin {id}: reutiliza el mismo segmento de recurso "control-horas/turnos-diarios" que
// ObtenerTurnoDiario (naming.md: "una query reutiliza el mismo segmento de recurso que ya usa el
// comando/query de ese recurso"). No hay colision de plantilla verificada en la investigacion del
// planner: no existe ningun POST sobre este mismo segmento en este dominio.
//
// Verificado empiricamente (projection-implementer, Marten 9.12.0 contra Postgres real via
// contenedor local desechable, sin dejar rastro en el repo): session.Query<TView>() SI traduce a
// SQL tanto el filtro de rango sobre DateOnly (v.Fecha >= desde && v.Fecha <= hasta) como su
// composicion con una igualdad de string anidada (v.Empleado.EmpleadoId == empleadoId) y el
// OrderBy(v => v.Fecha) subsiguiente -- cierra el "riesgo abierto" que la investigacion del planner
// dejaba pendiente para esta fase.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    private const string FormatoFecha = "yyyy-MM-dd";

    [Function("ListarTurnosDiarios")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/turnos-diarios")]
        HttpRequest req,
        CancellationToken ct)
    {
        // CA-2: desde/hasta son obligatorios -- ausencia o formato invalido devuelve 400. Igual que
        // ObtenerTurnoDiario (#289): DateOnly no liga directo desde el query string en el modelo
        // aislado de Functions, se parsea con formato explicito yyyy-MM-dd.
        if (!TryLeerFecha(req, "desde", out var desde, out var errorDesde))
            return new BadRequestObjectResult(errorDesde);

        if (!TryLeerFecha(req, "hasta", out var hasta, out var errorHasta))
            return new BadRequestObjectResult(errorHasta);

        // Rango invertido (hasta < desde): no cubierto por ningun CA del issue #290 a proposito
        // ("Propuesta revisable"). Se decide 400 -- coherente con el resto del contrato de
        // validacion de este endpoint (desde/hasta obligatorios -> 400), y evita devolver
        // silenciosamente una lista vacia ante lo que casi siempre es un error del cliente.
        if (hasta < desde)
            return new BadRequestObjectResult("El parametro 'hasta' no puede ser anterior a 'desde'");

        // empleadoId es opcional (CA-2): ausente = todos los empleados. StringValues.ToString()
        // devuelve cadena vacia cuando el parametro no viene, asi que ausente y vacio se tratan
        // igual -- sin filtro por empleado.
        var empleadoId = req.Query["empleadoId"].ToString();

        // CA-3/CA-4: recorte de la cota de 31 dias, siempre hacia adelante desde `desde`.
        var rangoAplicado = RangoConsulta.Recortar(desde, hasta);

        // CA-1: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
        // nunca a un tenant id que llegara por query string (mitigacion estructural contra
        // BOLA/IDOR, MEF-ADR-0028/skills/projections/read-apis.md). empleadoId/desde/hasta SI vienen
        // del query string: son el filtro del recurso, no el tenant.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // Propuesta revisable del planner adoptada: componer la query en pasos en vez de un unico
        // Where con el filtro de empleadoId embebido condicionalmente -- mas legible que anidar el
        // ternario dentro de la expresion LINQ.
        IQueryable<TurnoDiarioView> query = session.Query<TurnoDiarioView>()
            .Where(v => v.Fecha >= desde && v.Fecha <= rangoAplicado.HastaAplicado);

        if (!string.IsNullOrEmpty(empleadoId))
            query = query.Where(v => v.Empleado.EmpleadoId == empleadoId);

        // CA-5: los dias sin turno asignado se omiten -- la proyeccion solo materializa un
        // TurnoDiarioView por (empleado, fecha) con turno asignado, asi que un rango sin resultados
        // ya produce turnos: [] de forma natural, sin relleno de huecos (ver issue #290, seccion
        // "Los huecos: por que se omiten y no se rellenan").
        var vistas = await query
            .OrderBy(v => v.Fecha)
            .ToListAsync(ct);

        // CA-6: cada elemento es el mismo TurnoDiarioRespuesta de #289, sin el Id del documento.
        var turnos = vistas
            .Select(v => new TurnoDiarioRespuesta(v.Empleado, v.Fecha, v.DetalleTurno, v.UltimaSolicitudId))
            .ToList();

        var respuesta = new ListaTurnosDiarios(
            desde, rangoAplicado.HastaAplicado, rangoAplicado.RangoRecortado, turnos);

        // Nunca 404 (CA-5): sin resultados es 200 con turnos: [].
        return new OkObjectResult(respuesta);
    }

    // Ausente y mal formado comparten respuesta y mensaje a proposito: StringValues.ToString()
    // devuelve cadena vacia cuando el parametro no viene, y TryParseExact la rechaza igual que a
    // "31-12-2026". Un solo camino de fallo, un solo 400 (CA-2).
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
