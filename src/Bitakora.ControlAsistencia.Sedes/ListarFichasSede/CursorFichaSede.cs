using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Sedes.ListarFichasSede;

// Cursor opaco: JSON de la ultima fila en base64url. Solo este endpoint lo interpreta.
internal sealed record CursorFichaSede(string Codigo, string Id)
{
    public string Codificar() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));

    public static CursorFichaSede? Decodificar(string texto)
    {
        try
        {
            var cursor = JsonSerializer.Deserialize<CursorFichaSede>(
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(texto)));
            return cursor is { Codigo: not null, Id: not null } ? cursor : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
