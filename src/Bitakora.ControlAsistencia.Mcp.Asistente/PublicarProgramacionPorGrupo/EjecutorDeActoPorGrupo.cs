using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.PublicarProgramacionPorGrupo;

internal sealed record SolicitudDeActoPorGrupo(
    string? Sede, string? Etiquetas, bool? TodoLoPendiente, string? Desde, string? Hasta);

internal sealed record MensajesDeActoPorGrupo(
    string AlcanceObligatorio, string AlcanceExcluyente, string EtiquetaMalFormada, string SedeNoExiste,
    string SedeInactiva, string RechazoDelDominio, string FechaInvalida, string RangoIncompleto,
    string RangoInvertido, string RangoExcedeElMaximo, string AlcanceTodoLoPendiente, string SinRango);

internal sealed record ResultadoDeActoPorGrupo(
    string? Rechazo, string Alcance, string Rango, int Colaboradores,
    IReadOnlyList<ColaboradorFallidoDeGrupo>? Fallidos)
{
    public static ResultadoDeActoPorGrupo Rechazar(string rechazo) => new(rechazo, "", "", 0, null);
}

// Parte comun de publicar y descartar por grupo (MEF-ADR-0018): difieren solo en el acto por colaborador.
internal static class EjecutorDeActoPorGrupo
{
    internal const int MaximoDiasDelRango = 366;
    internal const int ActosSimultaneos = 8;

    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private sealed record Objetivo(string Codigo, string Nombre, DateOnly Desde, DateOnly Hasta);

    public static async Task<ResultadoDeActoPorGrupo> EjecutarAsync(
        SolicitudDeActoPorGrupo solicitud,
        ProgramacionApi programacion,
        ResolutorSedePorCodigo resolutorSedes,
        ResolutorCandidatosPorGrupo resolutorCandidatos,
        Func<string, DateOnly, DateOnly, CancellationToken, Task<HttpResponseMessage>> acto,
        TimeProvider reloj,
        MensajesDeActoPorGrupo mensajes,
        CancellationToken ct)
    {
        var conSelector = !string.IsNullOrWhiteSpace(solicitud.Sede) || !string.IsNullOrWhiteSpace(solicitud.Etiquetas);
        var todo = solicitud.TodoLoPendiente == true;
        if (todo && conSelector)
            return ResultadoDeActoPorGrupo.Rechazar(mensajes.AlcanceExcluyente);
        if (!todo && !conSelector)
            return ResultadoDeActoPorGrupo.Rechazar(mensajes.AlcanceObligatorio);

        SelectorDeGrupo? selectorInterpretado = null;
        var mensajesSelector = new MensajesDeSelector(
            mensajes.AlcanceObligatorio, mensajes.EtiquetaMalFormada, mensajes.SedeNoExiste,
            mensajes.SedeInactiva, mensajes.RechazoDelDominio);
        if (conSelector)
        {
            var (interpretado, rechazo) = SelectorDeGrupo.Interpretar(solicitud.Sede, solicitud.Etiquetas, mensajesSelector);
            if (rechazo is not null)
                return ResultadoDeActoPorGrupo.Rechazar(rechazo);
            selectorInterpretado = interpretado;
        }

        if (string.IsNullOrWhiteSpace(solicitud.Desde) != string.IsNullOrWhiteSpace(solicitud.Hasta))
            return ResultadoDeActoPorGrupo.Rechazar(mensajes.RangoIncompleto);

        DateOnly? desde = null, hasta = null;
        if (!string.IsNullOrWhiteSpace(solicitud.Desde))
        {
            if (!DateOnly.TryParseExact(
                solicitud.Desde, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return ResultadoDeActoPorGrupo.Rechazar(string.Format(mensajes.FechaInvalida, "desde", solicitud.Desde));
            if (!DateOnly.TryParseExact(
                solicitud.Hasta, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h))
                return ResultadoDeActoPorGrupo.Rechazar(string.Format(mensajes.FechaInvalida, "hasta", solicitud.Hasta));
            if (d > h)
                return ResultadoDeActoPorGrupo.Rechazar(mensajes.RangoInvertido);
            var dias = h.DayNumber - d.DayNumber + 1;
            if (dias > MaximoDiasDelRango)
                return ResultadoDeActoPorGrupo.Rechazar(string.Format(mensajes.RangoExcedeElMaximo, dias));
            desde = d;
            hasta = h;
        }

        SelectorDeGrupo? selector = null;
        if (selectorInterpretado is not null)
        {
            var (validado, rechazoSede) = await selectorInterpretado.ValidarSedeAsync(resolutorSedes, mensajesSelector, ct);
            if (rechazoSede is not null)
                return ResultadoDeActoPorGrupo.Rechazar(rechazoSede);
            selector = validado;
        }

        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), ZonaBogota).DateTime);
        var alcance = selector?.Descripcion ?? mensajes.AlcanceTodoLoPendiente;
        var rangoAplicado = desde is { } rd && hasta is { } rh
            ? $"{rd:yyyy-MM-dd} a {rh:yyyy-MM-dd}"
            : mensajes.SinRango;

        List<Objetivo> objetivos;
        if (selector is not null)
        {
            var candidatos = await resolutorCandidatos.ResolverAsync(
                desde ?? hoy, selector.CodigoSede, selector.Filtros, ct);
            if (candidatos.FalloDeLectura is { } falloFichas)
                return ResultadoDeActoPorGrupo.Rechazar(string.Format(mensajes.RechazoDelDominio, falloFichas));

            if (desde is { } dg && hasta is { } hg)
            {
                objetivos = [.. candidatos.Candidatos.Select(c => new Objetivo(c.CodigoColaborador, c.NombreCompleto, dg, hg))];
            }
            else
            {
                var codigos = candidatos.Candidatos.Select(c => c.CodigoColaborador).ToList();
                if (codigos.Count == 0)
                    return new ResultadoDeActoPorGrupo(null, alcance, rangoAplicado, 0, null);

                var pendientes = await LeerVistaAsync(programacion, hoy, codigos, null, null, ct);
                if (pendientes.Fallo is { } falloVista)
                    return ResultadoDeActoPorGrupo.Rechazar(string.Format(mensajes.RechazoDelDominio, falloVista));
                var delGrupo = codigos.ToHashSet();
                objetivos = [.. PendientesComoObjetivos(pendientes.Borradores.Where(b => delGrupo.Contains(b.CodigoColaborador)))];
            }
        }
        else
        {
            var pendientes = await LeerVistaAsync(programacion, hoy, null, desde, hasta, ct);
            if (pendientes.Fallo is { } falloVista)
                return ResultadoDeActoPorGrupo.Rechazar(string.Format(mensajes.RechazoDelDominio, falloVista));
            objetivos = desde is { } dt && hasta is { } ht
                ? [.. pendientes.Borradores
                    .Where(b => b.Dias.Count > 0)
                    .Select(b => new Objetivo(b.CodigoColaborador, b.NombreCompleto, dt, ht))]
                : [.. PendientesComoObjetivos(pendientes.Borradores)];
        }

        var exitosos = new ConcurrentBag<string>();
        var fallidos = new ConcurrentBag<ColaboradorFallidoDeGrupo>();
        await Parallel.ForEachAsync(
            objetivos,
            new ParallelOptions { MaxDegreeOfParallelism = ActosSimultaneos, CancellationToken = ct },
            async (objetivo, token) =>
            {
                foreach (var tramo in CorteEnTramos.Cortar(objetivo.Desde, objetivo.Hasta, MaximoDiasDelRango))
                {
                    var respuesta = await acto(objetivo.Codigo, tramo.Desde, tramo.Hasta, token);
                    if (await respuesta.LeerFalloAsync(token) is { } motivo)
                    {
                        fallidos.Add(new ColaboradorFallidoDeGrupo(objetivo.Codigo, objetivo.Nombre, motivo));
                        return;
                    }
                }

                exitosos.Add(objetivo.Codigo);
            });

        return new ResultadoDeActoPorGrupo(
            null, alcance, rangoAplicado, exitosos.Count,
            fallidos.IsEmpty
                ? null
                : [.. fallidos.OrderBy(f => f.CodigoColaborador, StringComparer.Ordinal)]);
    }

    private static IEnumerable<Objetivo> PendientesComoObjetivos(IEnumerable<BorradorDeProgramacion> borradores) =>
        borradores
            .Where(b => b.Dias.Count > 0)
            .Select(b => new Objetivo(
                b.CodigoColaborador, b.NombreCompleto, b.Dias.Min(d => d.Fecha), b.Dias.Max(d => d.Fecha)));

    private static async Task<(IReadOnlyList<BorradorDeProgramacion> Borradores, string? Fallo)> LeerVistaAsync(
        ProgramacionApi programacion, DateOnly hoy, IReadOnlyList<string>? codigos,
        DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var respuesta = await programacion.ListarBorradoresDeProgramacion(hoy, codigos, desde, hasta, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return ([], fallo);

        var borradores = (await respuesta.Content
            .ReadFromJsonAsync<SobreDeLista<BorradorDeProgramacion>>(OpcionesLectura, ct))?.Elementos ?? [];
        return (borradores, null);
    }
}
