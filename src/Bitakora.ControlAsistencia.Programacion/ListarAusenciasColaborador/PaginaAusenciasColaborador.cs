using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;

// Cursor opaco: JSON de la ultima fila en base64url. Solo este endpoint lo interpreta.
public sealed record CursorAusenciaColaborador(DateOnly PrimerDia, Guid Id)
{
    public string Codificar() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));

    public static CursorAusenciaColaborador? Decodificar(string texto)
    {
        try
        {
            return JsonSerializer.Deserialize<CursorAusenciaColaborador>(
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(texto)));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}

public sealed record ListaAusenciasColaboradorRespuesta(
    IReadOnlyList<AusenciaDelColaborador> Elementos, string? SiguienteCursor);

public static class PaginaAusenciasColaborador
{
    public static ListaAusenciasColaboradorRespuesta Componer(
        IReadOnlyList<AusenciaDelColaborador> vigentes, int? take, CursorAusenciaColaborador? cursor)
    {
        var ordenadas = vigentes
            .OrderBy(a => a.FechaInicio)
            .ThenBy(a => a.Id)
            .Where(a => cursor is not { } c
                || a.FechaInicio > c.PrimerDia
                || (a.FechaInicio == c.PrimerDia && a.Id.CompareTo(c.Id) > 0))
            .ToList();

        if (take is not { } tope || ordenadas.Count <= tope)
            return new ListaAusenciasColaboradorRespuesta(ordenadas, null);

        var pagina = ordenadas.Take(tope).ToList();
        var ultima = pagina[^1];
        return new ListaAusenciasColaboradorRespuesta(
            pagina, new CursorAusenciaColaborador(ultima.FechaInicio, ultima.Id).Codificar());
    }
}
