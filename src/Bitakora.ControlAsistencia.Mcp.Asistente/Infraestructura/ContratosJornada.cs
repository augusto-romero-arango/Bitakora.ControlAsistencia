using System.Globalization;
using System.Net.Http.Json;
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

public sealed record JornadaDelCatalogo(
    string JornadaId, string Descripcion, bool EsPredeterminada, bool Activa = true)
{
    public JornadaDescrita Describir() => new(JornadaId, Descripcion, EsPredeterminada);
}

public sealed record JornadaDescrita(
    [property: JsonPropertyName("jornada_id")] string JornadaId,
    string Descripcion,
    [property: JsonPropertyName("es_predeterminada")] bool EsPredeterminada)
{
    public JornadaDescritaResumen Resumir() => new(JornadaId, Descripcion);
}

public sealed record JornadaDescritaResumen(
    [property: JsonPropertyName("jornada_id")] string JornadaId, string Descripcion);

public static class ValidadorDuracion
{
    private const int HorasMaximas = 999;
    private const int MinutosMaximos = 59;

    public static bool TryParsear(string? texto, out HorasYMinutosJornada duracion)
    {
        duracion = new HorasYMinutosJornada(0, 0);
        var partes = texto?.Split(':');
        if (partes is not [{ Length: >= 1 and <= 3 } horasTexto, { Length: 2 } minutosTexto])
            return false;
        if (!horasTexto.All(char.IsAsciiDigit) || !minutosTexto.All(char.IsAsciiDigit))
            return false;

        var horas = int.Parse(horasTexto, CultureInfo.InvariantCulture);
        var minutos = int.Parse(minutosTexto, CultureInfo.InvariantCulture);
        if (horas > HorasMaximas || minutos > MinutosMaximos)
            return false;

        duracion = new HorasYMinutosJornada(horas, minutos);
        return true;
    }
}

internal static class LecturaDeJornada
{
    public static async Task<JornadaDescrita> LeerDescritaAsync(this HttpResponseMessage respuesta, CancellationToken ct) =>
        (await respuesta.Content.ReadFromJsonAsync<JornadaDelCatalogo>(ct))!.Describir();
}
