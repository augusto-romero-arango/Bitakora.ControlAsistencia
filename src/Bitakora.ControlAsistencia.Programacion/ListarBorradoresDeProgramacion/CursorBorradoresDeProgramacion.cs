using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Programacion.ListarBorradoresDeProgramacion;

// Cursor opaco: JSON de la ultima fila en base64url. Solo este endpoint lo interpreta.
public sealed record CursorBorradoresDeProgramacion(string NombreCompleto, string CodigoColaborador)
{
    public string Codificar() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));

    public static CursorBorradoresDeProgramacion? Decodificar(string texto)
    {
        try
        {
            var cursor = JsonSerializer.Deserialize<CursorBorradoresDeProgramacion>(
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(texto)));
            return cursor is { NombreCompleto: not null, CodigoColaborador: not null } ? cursor : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
