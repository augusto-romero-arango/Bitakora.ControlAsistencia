using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

// Function QUERY (RFC 10008, MEF-ADR-0042) que compone al leer sobre TurnoVigente (via (a') de MEF-ADR-0035).
// Keyset por CodigoColaborador, Take opcional (CA-ADR-0039).
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ListarMallaSemanalTurnos")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/malla-semanal-turnos")]
        HttpRequest req,
        CancellationToken ct)
    {
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        FiltroListarMallaSemanalTurnos? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarMallaSemanalTurnos>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        if (filtro.Fecha is null)
            return NoProcesable(Mensajes.Obtener("FechaObligatoria"));
        if (filtro.Colaboradores is not { Count: > 0 } colaboradores)
            return NoProcesable(Mensajes.Obtener("ColaboradoresObligatorios"));

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in colaboradores)
        {
            if (c is null || string.IsNullOrWhiteSpace(c.CodigoColaborador))
                return NoProcesable(Mensajes.Obtener("CodigoColaboradorObligatorio"));
            if (c.VigenteHasta is { } vh && vh < c.VigenteDesde)
                return NoProcesable(Mensajes.Obtener("VigenciaInvalida", c.CodigoColaborador));
            if (!vistos.Add(c.CodigoColaborador))
                return NoProcesable(Mensajes.Obtener("CodigoColaboradorRepetido", c.CodigoColaborador));
        }

        string? cursorCodigo = null;
        if (filtro.Cursor is not null)
        {
            if (!CursorOpaco.TryDecodificar(filtro.Cursor, out var decodificado))
                return NoProcesable(Mensajes.Obtener("CursorInvalido"));
            cursorCodigo = decodificado;
        }

        var semana = SemanaIso.De(filtro.Fecha.Value);
        var lunes = semana.Lunes;
        var domingo = semana.Domingo;
        var take = PaginaDeMallaSemanal.AcotarTake(filtro.Take);

        var pedidos = PaginaDeMallaSemanal.PosterioresAlCursor(
            colaboradores.OrderBy(c => c.CodigoColaborador, StringComparer.Ordinal), cursorCodigo);
        var pagina = take is null ? pedidos : pedidos.Take(take.Value).ToList();
        var codigos = pagina.Select(c => c.CodigoColaborador).ToList();

        // Sesion acotada al tenant que resuelve ITenantContext, nunca a un dato de la request (MEF-ADR-0028).
        await using var session = store.QuerySession(tenantContext.TenantId);

        var documentos = codigos.Count == 0
            ? []
            : await session.Query<TurnoVigente>()
                .Where(t => t.Fecha >= lunes && t.Fecha <= domingo && codigos.Contains(t.CodigoColaborador))
                .ToListAsync(ct);

        var filas = ComponedorMallaSemanal.Componer(semana, pagina, documentos);
        var siguienteCursor = take is not null && pedidos.Count > take.Value
            ? CursorOpaco.Codificar(filas[^1].CodigoColaborador)
            : null;

        return new OkObjectResult(new MallaSemanalTurnos(
            lunes, domingo, semana.Anio, semana.Numero, filas, siguienteCursor));
    }

    private static ObjectResult NoProcesable(string mensaje) =>
        new(mensaje) { StatusCode = StatusCodes.Status422UnprocessableEntity };
}
