using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
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
public class FunctionEndpoint(
    IDocumentStore store, ITenantContext tenantContext, IAseguradorJornadaPredeterminada asegurador)
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

        var predeterminadaId = await asegurador.AsegurarAsync(ct);

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

        var pagina = take is { } t
            ? await ordenada.Take(t + 1).ToListAsync(ct)
            : await ordenada.ToListAsync(ct);

        // Proyeccion Async atrasada: la predeterminada recien materializada se compone desde su stream.
        var idTexto = predeterminadaId.ToString();
        LimitesDeJornada? faltante = null;
        if (await session.LoadAsync<LimitesDeJornada>(idTexto, ct) is null)
        {
            var jornada = await session.Events.AggregateStreamAsync<Jornada>(idTexto, token: ct);
            faltante = jornada!.ComoVista();
        }

        return new OkObjectResult(ComposicionListadoLimites.Componer(pagina, faltante, cursor, take));
    }
}

public sealed record ListaLimitesDeJornadaRespuesta(
    IReadOnlyList<JornadaRespuesta> Elementos,
    string? SiguienteCursor);

// CA-3: cada elemento tiene la forma de ObtenerJornada; los minutos de la vista se traducen aqui.
internal static class LimitesDeJornadaRespuesta
{
    public static JornadaRespuesta DesdeVista(LimitesDeJornada vista) => new(
        Guid.Parse(vista.Id),
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

// Compone una pagina de la vista (o la vista completa) con la predeterminada que la proyeccion aun no
// materializo: la reubica en su posicion del orden y respeta cursor y take.
internal static class ComposicionListadoLimites
{
    public static ListaLimitesDeJornadaRespuesta Componer(
        IReadOnlyList<LimitesDeJornada> vista, LimitesDeJornada? predeterminada, CursorJornada? cursor, int? take,
        Guid? predeterminadaId = null)
    {
        IEnumerable<LimitesDeJornada> todas = predeterminada is null || vista.Any(l => l.Id == predeterminada.Id)
            ? vista
            : [.. vista, predeterminada];

        var ordenada = todas
            .OrderBy(l => l.HorasSemanalesEnMinutos)
            .ThenBy(l => l.TopeDiarioEnMinutos)
            .ThenBy(l => l.Id, StringComparer.Ordinal);

        var filtradas = cursor is { } c
            ? ordenada.Where(l =>
                l.HorasSemanalesEnMinutos > c.Horas
                || (l.HorasSemanalesEnMinutos == c.Horas && l.TopeDiarioEnMinutos > c.Tope)
                || (l.HorasSemanalesEnMinutos == c.Horas && l.TopeDiarioEnMinutos == c.Tope
                    && string.CompareOrdinal(l.Id, c.Id) > 0))
            : ordenada;

        var filas = take is { } t ? filtradas.Take(t + 1).ToList() : filtradas.ToList();

        string? siguienteCursor = null;
        if (take is { } tope && filas.Count > tope)
        {
            filas = filas.Take(tope).ToList();
            var ultima = filas[^1];
            siguienteCursor = new CursorJornada(
                ultima.HorasSemanalesEnMinutos, ultima.TopeDiarioEnMinutos, ultima.Id).Codificar();
        }

        return new ListaLimitesDeJornadaRespuesta(
            filas.Select(LimitesDeJornadaRespuesta.DesdeVista).ToList(), siguienteCursor);
    }
}
