using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class CierreDeLaVisperaActualizado
{
    public string Id { get; private set; } = null!;
    public DateTime? Cierre { get; private set; }

    private CierreDeLaVisperaActualizado(string id, DateTime? cierre)
    {
        Id = id;
        Cierre = cierre;
    }

    private CierreDeLaVisperaActualizado() { }

    public static CierreDeLaVisperaActualizado Crear(string id, DateTime? cierre) => new(id, cierre);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(CierreDeLaVisperaActualizado)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(CierreDeLaVisperaActualizado)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (CierreDeLaVisperaActualizado)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(CierreDeLaVisperaActualizado).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
