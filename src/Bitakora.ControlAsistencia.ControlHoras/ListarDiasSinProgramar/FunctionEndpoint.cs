using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

// Function QUERY (RFC 10008, MEF-ADR-0042) que compone al leer: cruza los dias de la ventana con los
// TurnoVigente existentes (via (a') de MEF-ADR-0035). Keyset por CodigoColaborador, Take opcional (CA-ADR-0039).
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ListarDiasSinProgramar")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/dias-sin-programar")]
        HttpRequest req,
        CancellationToken ct)
    {
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        FiltroListarDiasSinProgramar? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarDiasSinProgramar>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        if (filtro.DesdeFecha is null)
            return NoProcesable(Mensajes.Obtener("DesdeFechaObligatoria"));
        if (filtro.HastaFecha is null)
            return NoProcesable(Mensajes.Obtener("HastaFechaObligatoria"));
        if (filtro.HastaFecha < filtro.DesdeFecha)
            return NoProcesable(Mensajes.Obtener("HastaAnteriorADesde"));
        if (filtro.Colaboradores is not { Count: > 0 } colaboradores)
            return NoProcesable(Mensajes.Obtener("ColaboradoresObligatorios"));

        foreach (var c in colaboradores)
        {
            if (c is null || string.IsNullOrWhiteSpace(c.CodigoColaborador))
                return NoProcesable(Mensajes.Obtener("CodigoColaboradorObligatorio"));
            if (c.VigenteHasta is { } vh && vh < c.VigenteDesde)
                return NoProcesable(Mensajes.Obtener("VigenciaInvalida", c.CodigoColaborador));
        }

        string? cursorCodigo = null;
        if (filtro.Cursor is not null)
        {
            if (!CursorOpaco.TryDecodificar(filtro.Cursor, out var decodificado))
                return NoProcesable(Mensajes.Obtener("CursorInvalido"));
            cursorCodigo = decodificado;
        }

        var desde = filtro.DesdeFecha.Value;
        var ventana = VentanaConsulta.Recortar(desde, filtro.HastaFecha.Value);
        var hasta = ventana.Hasta;
        var take = PaginaDeDiasSinProgramar.AcotarTake(filtro.Take);

        var pedidos = PaginaDeDiasSinProgramar.PosterioresAlCursor(colaboradores, cursorCodigo);
        var codigos = pedidos.Select(c => c.CodigoColaborador).Distinct().ToList();

        // Sesion acotada al tenant que resuelve ITenantContext, nunca a un dato de la request (MEF-ADR-0028).
        await using var session = store.QuerySession(tenantContext.TenantId);

        var existentes = codigos.Count == 0
            ? []
            : await session.Query<TurnoVigente>()
                .Where(t => t.Fecha >= desde && t.Fecha <= hasta && codigos.Contains(t.CodigoColaborador))
                .Select(t => new { t.CodigoColaborador, t.Fecha })
                .ToListAsync(ct);

        var calculados = CalculadorDiasSinProgramar.Calcular(
            desde, hasta, pedidos, existentes.Select(e => (e.CodigoColaborador, e.Fecha)));

        var (pagina, siguienteCursor) = PaginaDeDiasSinProgramar.Cortar(calculados, take, f => f.CodigoColaborador);

        return new OkObjectResult(new ListaDiasSinProgramar(desde, hasta, ventana.RangoRecortado, pagina, siguienteCursor));
    }

    private static ObjectResult NoProcesable(string mensaje) =>
        new(mensaje) { StatusCode = StatusCodes.Status422UnprocessableEntity };
}
