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
