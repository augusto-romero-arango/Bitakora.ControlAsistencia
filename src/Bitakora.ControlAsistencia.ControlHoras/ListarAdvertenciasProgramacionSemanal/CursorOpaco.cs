using System.Text;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

/// <summary>Cursor opaco (CA-ADR-0039): el codigo del ultimo colaborador en base64url.</summary>
public static class CursorOpaco
{
    public static string Codificar(string codigoColaborador) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(codigoColaborador))
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
            codigoColaborador = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(base64));
            return codigoColaborador.Length > 0;
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return false;
        }
    }
}
