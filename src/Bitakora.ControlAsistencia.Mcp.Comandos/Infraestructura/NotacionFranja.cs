using System.Globalization;
using System.Text;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Formato HH:mm de las franjas, en las dos direcciones: lo que entra por parametro de tool
// (TryParseHora) y lo que sale en el eco (Compactar). Compartido por las tools de diseno de turno
// (#609-#611). Replica el formato HH:mm[+N]-HH:mm[+N] que ObtenerTurnoTool.Compactar ya usa en
// Mcp.Consultas -- se replica el texto, no el tipo (MEF-ADR-0047 decision 3: cada servidor MCP es
// una isla).
internal static class NotacionFranja
{
    private static readonly string[] FormatosAceptados = ["HH:mm", "H:mm"];

    public static bool TryParseHora(string valor, out TimeOnly hora) =>
        TimeOnly.TryParseExact(
            valor, FormatosAceptados, CultureInfo.InvariantCulture, DateTimeStyles.None, out hora);

    public static string Compactar(
        TimeOnly inicio,
        TimeOnly fin,
        int diaOffsetFin,
        IReadOnlyList<SubFranjaFicha> descansos,
        IReadOnlyList<SubFranjaFicha> extras,
        string? nombreSede)
    {
        var texto = new StringBuilder(Rango(inicio, fin, diaOffsetInicio: 0, diaOffsetFin));

        foreach (var descanso in descansos)
            texto.Append(
                $", descanso {Rango(descanso.HoraInicio, descanso.HoraFin, descanso.DiaOffsetInicio, descanso.DiaOffsetFin)}");

        foreach (var extra in extras)
            texto.Append(
                $", extra {Rango(extra.HoraInicio, extra.HoraFin, extra.DiaOffsetInicio, extra.DiaOffsetFin)}");

        if (nombreSede is not null)
            texto.Append($", sede: {nombreSede}");

        return texto.ToString();
    }

    public static string Hora(TimeOnly hora) => hora.ToString("HH\\:mm");

    // Publico (dentro del alcance del internal contenedor): agregar_subfranja/quitar_subfranja
    // (#610) lo reusan para componer el eco de una sub-franja suelta, sin la etiqueta
    // "descanso"/"extra" que Compactar antepone.
    public static string Rango(TimeOnly inicio, TimeOnly fin, int diaOffsetInicio, int diaOffsetFin) =>
        $"{Hora(inicio)}{Sufijo(diaOffsetInicio)}-{Hora(fin)}{Sufijo(diaOffsetFin)}";

    private static string Sufijo(int diaOffset) => diaOffset > 0 ? $"+{diaOffset}" : "";
}
