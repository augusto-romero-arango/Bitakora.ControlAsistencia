using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Programacion.ListarFichasTurno;

// Cursor opaco: JSON de la ultima fila en base64url. Solo este endpoint lo interpreta.
internal sealed record CursorFichaTurno(string Nombre, string Id)
{
    public string Codificar() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));

    public static CursorFichaTurno? Decodificar(string texto)
    {
        try
        {
            var cursor = JsonSerializer.Deserialize<CursorFichaTurno>(
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(texto)));
            return cursor is { Nombre: not null, Id: not null } ? cursor : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
