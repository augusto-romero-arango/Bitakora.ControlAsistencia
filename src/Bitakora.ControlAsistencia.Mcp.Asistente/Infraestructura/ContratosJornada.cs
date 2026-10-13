using System.Text.Json.Serialization;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed record HorasYMinutosJornada(int Horas, int Minutos);

public sealed record LimitesJornadaAEnviar(
    HorasYMinutosJornada HorasSemanales,
    HorasYMinutosJornada TopeDiario,
    HorasYMinutosJornada MinimoDiario,
    int DiasDescansoPorSemana);

public sealed record JornadaACrear(
    Guid JornadaId,
    HorasYMinutosJornada HorasSemanales,
    HorasYMinutosJornada TopeDiario,
    HorasYMinutosJornada MinimoDiario,
    int DiasDescansoPorSemana);

public sealed record FichaJornada(string JornadaId, string Descripcion, bool EsPredeterminada);

public sealed record JornadaDescrita(
    [property: JsonPropertyName("jornada_id")] string JornadaId,
    string Descripcion,
    [property: JsonPropertyName("es_predeterminada")] bool EsPredeterminada);

public static class ValidadorDuracion
{
    public static bool TryParsear(string? texto, out HorasYMinutosJornada duracion) =>
        throw new NotImplementedException();
}
