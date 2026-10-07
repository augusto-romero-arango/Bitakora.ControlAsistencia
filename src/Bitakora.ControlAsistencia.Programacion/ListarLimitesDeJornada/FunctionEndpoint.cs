using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarLimitesDeJornada;

// Listado de la vista LimitesDeJornada: keyset por (HorasSemanalesEnMinutos, TopeDiarioEnMinutos,
// Id), take opcional (max 200), sobre { elementos, siguienteCursor } con cursor opaco, sin total
// (CA-ADR-0039). Comparte la ruta con CrearJornada (POST); cada uno declara su verbo.
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    private const int TakeMaximo = 200;

    [Function("ListarLimitesDeJornada")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/jornadas")]
        HttpRequest req,
        CancellationToken ct)
    {
        int? take = null;
        if (req.Query.TryGetValue("take", out var takeCrudo) && takeCrudo.Count > 0)
        {
            if (!int.TryParse(takeCrudo[0], out var valor) || valor < 1 || valor > TakeMaximo)
                return new BadRequestObjectResult($"take debe ser un entero entre 1 y {TakeMaximo}");
            take = valor;
        }

        CursorJornada? cursor = null;
        if (req.Query.TryGetValue("cursor", out var cursorCrudo) && !string.IsNullOrEmpty(cursorCrudo[0]))
        {
            cursor = CursorJornada.Decodificar(cursorCrudo[0]!);
            if (cursor is null)
                return new BadRequestObjectResult("El cursor no es valido");
        }

        // MEF-ADR-0028: QuerySession acotada al tenant de ITenantContext, nunca a uno del request.
        await using var session = store.QuerySession(tenantContext.TenantId);

        IQueryable<LimitesDeJornada> query = session.Query<LimitesDeJornada>();
        if (cursor is { } c)
        {
            query = query.Where(l =>
                l.HorasSemanalesEnMinutos > c.Horas
                || (l.HorasSemanalesEnMinutos == c.Horas && l.TopeDiarioEnMinutos > c.Tope)
                || (l.HorasSemanalesEnMinutos == c.Horas && l.TopeDiarioEnMinutos == c.Tope
                    && l.Id.CompareTo(c.Id) > 0));
        }

        var ordenada = query
            .OrderBy(l => l.HorasSemanalesEnMinutos)
            .ThenBy(l => l.TopeDiarioEnMinutos)
            .ThenBy(l => l.Id);

        // Se pide take + 1 para saber si hay mas sin contar el total.
        var filas = take is { } t
            ? await ordenada.Take(t + 1).ToListAsync(ct)
            : await ordenada.ToListAsync(ct);

        string? siguienteCursor = null;
        if (take is { } tope && filas.Count > tope)
        {
            filas = filas.Take(tope).ToList();
            var ultima = filas[^1];
            siguienteCursor = new CursorJornada(
                ultima.HorasSemanalesEnMinutos, ultima.TopeDiarioEnMinutos, ultima.Id).Codificar();
        }

        return new OkObjectResult(new ListaLimitesDeJornadaRespuesta(
            filas.Select(ElementoRespuesta.DesdeVista).ToList(), siguienteCursor));
    }
}

public sealed record ListaLimitesDeJornadaRespuesta(
    IReadOnlyList<ElementoRespuesta> Elementos,
    string? SiguienteCursor);

public sealed record ElementoRespuesta(
    string Id,
    HorasYMinutosRespuesta HorasSemanales,
    HorasYMinutosRespuesta TopeDiario,
    HorasYMinutosRespuesta MinimoDiario,
    int DiasDescansoPorSemana,
    string Descripcion)
{
    public static ElementoRespuesta DesdeVista(LimitesDeJornada vista) => new(
        vista.Id,
        Convertir(vista.HorasSemanalesEnMinutos),
        Convertir(vista.TopeDiarioEnMinutos),
        Convertir(vista.MinimoDiarioEnMinutos),
        vista.DiasDescansoPorSemana,
        vista.Descripcion);

    private static HorasYMinutosRespuesta Convertir(int minutos) => new(minutos / 60, minutos % 60);
}

// Cursor opaco: JSON de la ultima fila en base64url. Solo este endpoint lo interpreta.
internal sealed record CursorJornada(int Horas, int Tope, string Id)
{
    public string Codificar() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));

    public static CursorJornada? Decodificar(string texto)
    {
        try
        {
            var cursor = JsonSerializer.Deserialize<CursorJornada>(
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(texto)));
            return cursor is { Id: not null } ? cursor : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
