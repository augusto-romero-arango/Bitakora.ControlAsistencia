using System.Net;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>Turno creado/reutilizado/completado por una entrada franja; accion: creo, reutilizo o completo.</summary>
public sealed record TurnoInlineResumen(string Nombre, string Accion);

/// <summary>Mensajes de la tool llamadora: {0} nombre del turno; FalloTurnoInline {1} motivo, {2} creados.</summary>
internal sealed record MensajesDeTurnoEnLinea(
    string RechazoDelDominio,
    string EnConflicto,
    string NombreDuplicado,
    string Fallo,
    string NingunoCreado);

/// <summary>Resultado: o un Rechazo (texto para el asistente) o los turnos listos por nombre derivado.</summary>
internal sealed record TurnosEnLineaPreparados(
    string? Rechazo,
    IReadOnlyDictionary<string, string> TurnoIdPorNombre,
    IReadOnlyList<TurnoInlineResumen> Resumen);

internal sealed record FranjaInline(TimeOnly Inicio, TimeOnly Fin)
{
    public int? DiaOffsetEnviado => Inicio == Fin ? 1 : null;

    private int DiaOffsetFin => Fin <= Inicio ? 1 : 0;

    public string Nombre => NotacionFranja.Rango(Inicio, Fin, 0, DiaOffsetFin);

    public bool Equivale(FranjaFicha f) =>
        f.HoraInicio == Inicio && f.HoraFin == Fin && f.DiaOffsetFin == DiaOffsetFin
        && f.Descansos.Count == 0 && f.Extras.Count == 0 && f.SedeId is null;

    public static bool TryParsear(string texto, out FranjaInline inline)
    {
        inline = null!;
        var partes = texto.Split('-');
        if (partes.Length != 2
            || !NotacionFranja.TryParseHora(partes[0].Trim(), out var inicio)
            || !NotacionFranja.TryParseHora(partes[1].Trim(), out var fin))
            return false;

        inline = new FranjaInline(inicio, fin);
        return true;
    }
}

// Resuelve el turno nombrado con la franja (nombre derivado, clave de unicidad del catalogo): lo crea
// si no existe, lo completa si esta vacio, lo reutiliza si ya tiene esa franja y aborta si existe con
// otro contenido. Compartido por crear_plantilla y crear_plantilla_semanal (MEF-ADR-0018).
internal sealed class PreparadorDeTurnosEnLinea(ProgramacionApi programacion, ResolutorTurnoPorNombre resolutor)
{
    private enum ClaseInline { Nuevo, Equivalente, Vacio, Distinto }

    private static readonly Dictionary<ClaseInline, string> AccionPorClase = new()
    {
        [ClaseInline.Nuevo] = "creo",
        [ClaseInline.Equivalente] = "reutilizo",
        [ClaseInline.Vacio] = "completo"
    };

    public async Task<TurnosEnLineaPreparados> PrepararAsync(
        IReadOnlyList<FranjaInline> franjas, MensajesDeTurnoEnLinea mensajes, CancellationToken ct)
    {
        var turnoIdPorNombre = new Dictionary<string, string>();
        var resumen = new List<TurnoInlineResumen>();
        if (franjas.Count == 0)
            return new TurnosEnLineaPreparados(null, turnoIdPorNombre, resumen);

        var resolucion = await resolutor.ResolverVariosAsync(franjas.Select(f => f.Nombre), ct);
        if (resolucion.FalloDeLectura is { } falloCatalogo)
            return Rechazado(string.Format(mensajes.RechazoDelDominio, falloCatalogo));

        var clasificadas = franjas
            .Zip(resolucion.Resoluciones, (f, r) => (Inline: f, r.Ficha))
            .DistinctBy(x => x.Inline.Nombre)
            .Select(x => (x.Inline, x.Ficha, Clase: Clasificar(x.Inline, x.Ficha)))
            .ToList();

        if (clasificadas.FirstOrDefault(x => x.Clase == ClaseInline.Distinto).Inline is { } enConflicto)
            return Rechazado(string.Format(mensajes.EnConflicto, enConflicto.Nombre));

        var creados = new List<string>();

        foreach (var (inline, ficha, clase) in clasificadas)
        {
            var turnoId = ficha?.Id;

            if (clase == ClaseInline.Nuevo)
            {
                var nuevoId = Guid.CreateVersion7();
                var respuestaCrear = await programacion.CrearTurno(nuevoId, inline.Nombre, false, ct);
                if (await respuestaCrear.LeerFalloAsync(ct) is { } falloCrear)
                    return Rechazado(respuestaCrear.StatusCode == HttpStatusCode.Conflict
                        ? string.Format(mensajes.NombreDuplicado, inline.Nombre, Listar(creados, mensajes))
                        : string.Format(mensajes.Fallo, inline.Nombre, falloCrear, Listar(creados, mensajes)));
                creados.Add(inline.Nombre);
                turnoId = nuevoId.ToString();
            }

            if (clase != ClaseInline.Equivalente)
            {
                var respuestaFranja = await programacion.AgregarFranja(
                    turnoId!, new FranjaAAgregar(inline.Inicio, inline.Fin, inline.DiaOffsetEnviado, null), ct);
                if (await respuestaFranja.LeerFalloAsync(ct) is { } falloFranja)
                    return Rechazado(string.Format(mensajes.Fallo, inline.Nombre, falloFranja, Listar(creados, mensajes)));
            }

            resumen.Add(new TurnoInlineResumen(inline.Nombre, AccionPorClase[clase]));
            turnoIdPorNombre[inline.Nombre] = turnoId!;
        }

        return new TurnosEnLineaPreparados(null, turnoIdPorNombre, resumen);
    }

    private static TurnosEnLineaPreparados Rechazado(string texto) =>
        new(texto, new Dictionary<string, string>(), []);

    private static string Listar(List<string> creados, MensajesDeTurnoEnLinea mensajes) =>
        creados.Count > 0 ? string.Join(", ", creados) : mensajes.NingunoCreado;

    private static ClaseInline Clasificar(FranjaInline inline, FichaTurno? ficha) =>
        ficha switch
        {
            null => ClaseInline.Nuevo,
            { EsDescanso: true } => ClaseInline.Distinto,
            { Franjas.Count: 0 } => ClaseInline.Vacio,
            { Franjas: [var f] } when inline.Equivale(f) => ClaseInline.Equivalente,
            _ => ClaseInline.Distinto
        };
}
