namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Extraido para las tools de diseno de turno (#609-#611): agregar_franja/quitar_franja (este
// issue) y las que #610/#611 agreguen despues. Replica el formato HH:mm[+N]-HH:mm[+N] que
// ObtenerTurnoTool.Compactar ya usa en Mcp.Consultas -- se replica el texto, no el tipo
// (MEF-ADR-0047 decision 3: cada servidor MCP es una isla).
internal static class NotacionFranja
{
    public static string Compactar(
        TimeOnly inicio,
        TimeOnly fin,
        int diaOffsetFin,
        IReadOnlyList<SubFranjaFicha> descansos,
        IReadOnlyList<SubFranjaFicha> extras,
        string? nombreSede) =>
        throw new NotImplementedException();
}
