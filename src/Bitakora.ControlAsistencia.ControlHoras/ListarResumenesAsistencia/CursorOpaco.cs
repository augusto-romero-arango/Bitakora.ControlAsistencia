using System.Text;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>Cursor opaco (CA-ADR-0039): las claves keyset serializadas en JSON + base64url.</summary>
public static class CursorOpaco
{
    private sealed record Claves(string C);

    public static string Codificar(string codigoColaborador) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Claves(codigoColaborador)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecodificar(string cursor, out string codigoColaborador)
    {
        codigoColaborador = string.Empty;
        if (string.IsNullOrEmpty(cursor))
            return false;

        var base64 = cursor.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        try
        {
            var json = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(base64));
            var claves = JsonSerializer.Deserialize<Claves>(json);
            if (string.IsNullOrEmpty(claves?.C))
                return false;
            codigoColaborador = claves.C;
            return true;
        }
        catch (Exception e) when (e is FormatException or ArgumentException or JsonException)
        {
            return false;
        }
    }
}
