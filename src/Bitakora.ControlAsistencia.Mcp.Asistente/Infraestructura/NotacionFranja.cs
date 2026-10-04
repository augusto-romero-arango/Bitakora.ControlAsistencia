namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

internal static class NotacionFranja
{
    public static bool TryParseHora(string valor, out TimeOnly hora) =>
        throw new NotImplementedException();

    public static string Compactar(
        TimeOnly inicio,
        TimeOnly fin,
        int diaOffsetFin,
        IReadOnlyList<SubFranjaFicha> descansos,
        IReadOnlyList<SubFranjaFicha> extras,
        string? nombreSede) =>
        throw new NotImplementedException();

    public static string Hora(TimeOnly hora) => throw new NotImplementedException();

    public static string Rango(TimeOnly inicio, TimeOnly fin, int diaOffsetInicio, int diaOffsetFin) =>
        throw new NotImplementedException();
}
