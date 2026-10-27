using System.Text.Json;
using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurno;

internal sealed record FranjaLeida(
    TimeOnly Inicio,
    TimeOnly Fin,
    IReadOnlyList<(TimeOnly Inicio, TimeOnly Fin)> Descansos,
    IReadOnlyList<(TimeOnly Inicio, TimeOnly Fin)> Extras,
    string? CodigoSede)
{
    // Solo la franja de 24 h exactas necesita diaOffsetFin: el dominio infiere el resto.
    private int? OffsetAEnviar => Inicio == Fin ? 1 : null;

    public OrdinariaACrear ParaEnviar(SedeProgramada? sede) => new(
        NotacionFranja.Hora(Inicio),
        NotacionFranja.Hora(Fin),
        OffsetAEnviar,
        Descansos.Select(d => new SubFranjaACrear(NotacionFranja.Hora(d.Inicio), NotacionFranja.Hora(d.Fin))).ToList(),
        Extras.Select(e => new SubFranjaACrear(NotacionFranja.Hora(e.Inicio), NotacionFranja.Hora(e.Fin))).ToList(),
        sede);

    public string Compactar(string? nombreSede) => NotacionFranja.Compactar(
        Inicio,
        Fin,
        Fin <= Inicio ? 1 : 0,
        Descansos.Select(Hija).ToList(),
        Extras.Select(Hija).ToList(),
        nombreSede);

    private static SubFranjaFicha Hija((TimeOnly Inicio, TimeOnly Fin) h) =>
        new(h.Inicio, h.Fin, 0, h.Fin <= h.Inicio ? 1 : 0);
}

internal sealed record LecturaDeFranjas(IReadOnlyList<FranjaLeida> Franjas, string? Error);

internal static class FranjasDeTurno
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private sealed record Entrada(
        string? Franja,
        List<string>? Descansos,
        List<string>? Extras,
        [property: JsonPropertyName("codigo_sede")] string? CodigoSede);

    public static LecturaDeFranjas Leer(string json)
    {
        List<Entrada>? entradas;
        try
        {
            entradas = JsonSerializer.Deserialize<List<Entrada>>(json, OpcionesLectura);
        }
        catch (JsonException)
        {
            return Fallo(CrearTurnoTool.Mensajes.FranjasJsonInvalido);
        }

        if (entradas is null || entradas.Any(e => e is null))
            return Fallo(CrearTurnoTool.Mensajes.FranjasJsonInvalido);

        var franjas = new List<FranjaLeida>();
        for (var i = 0; i < entradas.Count; i++)
        {
            var posicion = i + 1;
            var entrada = entradas[i];
            if (string.IsNullOrWhiteSpace(entrada.Franja))
                return Fallo(string.Format(CrearTurnoTool.Mensajes.FranjaObligatoria, posicion));
            if (!TryRango(entrada.Franja, out var rango))
                return Fallo(string.Format(CrearTurnoTool.Mensajes.FranjaInvalida, posicion, entrada.Franja));

            var descansos = new List<(TimeOnly, TimeOnly)>();
            foreach (var texto in entrada.Descansos ?? [])
            {
                if (!TryRango(texto, out var d))
                    return Fallo(string.Format(CrearTurnoTool.Mensajes.DescansoInvalido, posicion, texto));
                descansos.Add(d);
            }

            var extras = new List<(TimeOnly, TimeOnly)>();
            foreach (var texto in entrada.Extras ?? [])
            {
                if (!TryRango(texto, out var x))
                    return Fallo(string.Format(CrearTurnoTool.Mensajes.ExtraInvalido, posicion, texto));
                extras.Add(x);
            }

            var sede = string.IsNullOrWhiteSpace(entrada.CodigoSede) ? null : entrada.CodigoSede.Trim();
            franjas.Add(new FranjaLeida(rango.Inicio, rango.Fin, descansos, extras, sede));
        }

        return new LecturaDeFranjas(franjas, null);
    }

    private static LecturaDeFranjas Fallo(string error) => new([], error);

    private static bool TryRango(string? texto, out (TimeOnly Inicio, TimeOnly Fin) rango)
    {
        rango = default;
        var partes = (texto ?? string.Empty).Split('-');
        if (partes.Length != 2
            || !NotacionFranja.TryParseHora(partes[0].Trim(), out var inicio)
            || !NotacionFranja.TryParseHora(partes[1].Trim(), out var fin))
            return false;

        rango = (inicio, fin);
        return true;
    }
}
