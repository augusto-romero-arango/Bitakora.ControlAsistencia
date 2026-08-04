using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoDiario;

// Issue #289: primera Function GET del BC (skills/projections/naming.md, MEF-ADR-0006 enmienda
// #363, via (a) proyeccion materializada). Feature folder sin sufijo Function, un namespace por
// query (skills/projections/read-apis.md): esta clase FunctionEndpoint no colisiona con las otras
// cuatro homonimas del ensamblado (RegistrarMarcacionFunction, AdicionarMarcacionCuando...,
// AsignarTurnoCuando...) porque cada una vive en su propio namespace.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    private const string FormatoFecha = "yyyy-MM-dd";

    [Function("ObtenerTurnoDiario")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/turnos-diarios/{empleadoId}/{fecha}")]
        HttpRequest req,
        string empleadoId,
        string fecha,
        CancellationToken ct)
    {
        // Fecha no liga directo a DateOnly en el modelo aislado de Functions: se recibe como
        // string y se parsea con formato explicito, devolviendo 400 ante formato invalido
        // (nota tecnica del issue #289).
        if (!DateOnly.TryParseExact(
                fecha, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaParseada))
            return new BadRequestObjectResult(
                $"El parametro 'fecha' debe tener el formato {FormatoFecha}");

        var streamKey = ControlDiarioAggregateRoot.ComputarStreamId(empleadoId, fechaParseada);

        // CA-5: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
        // nunca a un tenant id que llegara por ruta o query string (mitigacion estructural contra
        // BOLA/IDOR, MEF-ADR-0028/skills/projections/read-apis.md). empleadoId y fecha SI vienen de
        // la ruta: son el recurso, no el tenant.
        await using var session = store.QuerySession(tenantResolver.TenantId);
        var vista = await session.LoadAsync<TurnoDiarioView>(streamKey, ct);

        // CA-6: 404 sin body cuando no hay turno vigente para ese (empleado, fecha) -- no es un
        // error, significa que ese dia no tiene turno asignado.
        if (vista is null)
            return new NotFoundResult();

        var respuesta = new TurnoDiarioRespuesta(
            vista.Empleado, vista.Fecha, vista.DetalleTurno, vista.UltimaSolicitudId);

        return new OkObjectResult(respuesta);
    }
}
