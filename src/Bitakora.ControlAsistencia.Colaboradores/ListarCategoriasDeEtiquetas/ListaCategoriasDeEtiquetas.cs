using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;

namespace Bitakora.ControlAsistencia.Colaboradores.ListarCategoriasDeEtiquetas;

public sealed record ListaCategoriasDeEtiquetas(IReadOnlyList<CategoriaDeEtiquetas> Elementos, string? SiguienteCursor);

public static class CursorCategoria
{
    private sealed record Clave(string Id);

    public static string Codificar(string id) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Clave(id))));

    public static bool TryDecodificar(string cursor, out string id)
    {
        id = string.Empty;
        try
        {
            var clave = JsonSerializer.Deserialize<Clave>(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)));
            if (clave is not { Id: not null })
                return false;
            id = clave.Id;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }
}
