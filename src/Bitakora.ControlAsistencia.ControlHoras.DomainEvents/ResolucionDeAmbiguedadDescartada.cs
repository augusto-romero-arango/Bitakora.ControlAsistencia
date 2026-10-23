using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class ResolucionDeAmbiguedadDescartada
{
    public string Id { get; private set; } = null!;
    public DiaVecino Vecino { get; private set; }

    private ResolucionDeAmbiguedadDescartada(string id, DiaVecino vecino)
    {
        Id = id;
        Vecino = vecino;
    }

    private ResolucionDeAmbiguedadDescartada() { }

    public static ResolucionDeAmbiguedadDescartada Crear(string id, DiaVecino vecino) => new(id, vecino);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(ResolucionDeAmbiguedadDescartada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(ResolucionDeAmbiguedadDescartada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (ResolucionDeAmbiguedadDescartada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(ResolucionDeAmbiguedadDescartada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
