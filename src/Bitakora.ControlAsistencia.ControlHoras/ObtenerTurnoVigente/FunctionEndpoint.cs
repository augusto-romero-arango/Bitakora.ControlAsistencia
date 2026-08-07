using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoVigente;

// Issue #328: Function GET del read model TurnoVigente (via (a) proyeccion materializada,
// skills/projections/naming.md, MEF-ADR-0006 enmienda #363). Feature folder sin sufijo Function, un
// namespace por query (skills/projections/read-apis.md): esta clase FunctionEndpoint no colisiona
// con ListarTurnosVigentes/RegistrarMarcacionFunction/... porque cada una vive en su propio
// namespace.
//
// La ruta recibe empleadoId y fecha como los componentes tipados de la clave natural compuesta
// -- la clave se reconstruye con ControlDiarioAggregateRoot.ComputarStreamId, nunca con una
// concatenacion propia del endpoint (MEF-ADR-0037). CA-4: fecha invalida -> 400 explicito; 200 con
// la vista completa (Id incluido -- la UI lo necesita como ancla de comandos, ver issue "Notas
// tecnicas") o 404 sin body cuando no hay turno vigente.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    private const string FormatoFecha = "yyyy-MM-dd";

    [Function("ObtenerTurnoVigente")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/turnos-vigentes/{empleadoId}/{fecha}")]
        HttpRequest req,
        string empleadoId,
        string fecha,
        CancellationToken ct)
    {
        // Fecha no liga directo a DateOnly en el modelo aislado de Functions: se recibe como
        // string y se parsea con formato explicito, devolviendo 400 ante formato invalido
        // (borde HTTP con parseo tipado, MEF-ADR-0037).
        if (!DateOnly.TryParseExact(
                fecha, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaParseada))
            return new BadRequestObjectResult(
                $"El parametro 'fecha' debe tener el formato {FormatoFecha}");

        var streamKey = ControlDiarioAggregateRoot.ComputarStreamId(empleadoId, fechaParseada);

        // CA-4: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
        // nunca a un tenant id que llegara por ruta o query string (mitigacion estructural contra
        // BOLA/IDOR, MEF-ADR-0028/skills/projections/read-apis.md). empleadoId y fecha SI vienen de
        // la ruta: son el recurso, no el tenant.
        await using var session = store.QuerySession(tenantResolver.TenantId);
        var vista = await session.LoadAsync<TurnoVigente>(streamKey, ct);

        // CA-4: 404 sin body cuando no hay turno vigente para ese (empleado, fecha) -- no es un
        // error, significa que ese dia no tiene turno asignado.
        if (vista is null)
            return new NotFoundResult();

        return new OkObjectResult(vista);
    }
}
