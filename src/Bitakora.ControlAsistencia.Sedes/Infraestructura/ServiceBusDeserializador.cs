using System.Text.Json;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// Wolverine serializa en camelCase; ToObjectFromJson sin opciones es case-sensitive y deja todas las
// propiedades en null.
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
