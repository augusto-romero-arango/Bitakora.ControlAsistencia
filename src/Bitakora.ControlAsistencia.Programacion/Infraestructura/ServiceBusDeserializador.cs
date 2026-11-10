using System.Text.Json;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

/// <summary>
/// Wolverine serializa en camelCase: sin PropertyNameCaseInsensitive todas las propiedades quedarian null.
/// </summary>
public static class ServiceBusDeserializador
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static T Deserializar<T>(BinaryData body)
        => JsonSerializer.Deserialize<T>(body.ToString(), Opciones)
           ?? throw new InvalidOperationException(
               $"No se pudo deserializar el mensaje como {typeof(T).Name}");
}
