using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class PlantillaDeTurnosRetirada
{
    public Guid PlantillaId { get; private set; }

    private PlantillaDeTurnosRetirada(Guid plantillaId) => PlantillaId = plantillaId;

    private PlantillaDeTurnosRetirada() { }

    public static PlantillaDeTurnosRetirada Crear(Guid plantillaId) => new(plantillaId);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(PlantillaDeTurnosRetirada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(PlantillaDeTurnosRetirada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (PlantillaDeTurnosRetirada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(PlantillaDeTurnosRetirada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
