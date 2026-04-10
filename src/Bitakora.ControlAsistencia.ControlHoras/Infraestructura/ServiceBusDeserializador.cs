using System.Text.Json;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

/// <summary>
/// Helper para deserializar mensajes de Service Bus con opciones correctas.
/// Wolverine serializa con camelCase; ToObjectFromJson sin opciones usa
/// PascalCase (case-sensitive), lo que causa que todas las propiedades queden null.
/// </summary>
public static class ServiceBusDeserializador
{
    public static T Deserializar<T>(BinaryData body)
        => throw new NotImplementedException();
}
