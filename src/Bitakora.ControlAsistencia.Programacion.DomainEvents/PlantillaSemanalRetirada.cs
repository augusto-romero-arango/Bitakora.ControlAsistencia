using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Retiro de una plantilla semanal: deja de ser usable (CA-ADR-0034 decision 4, espejo de
// TurnoRetirado). El stream conserva la memoria -- nada se borra del event store.
public sealed class PlantillaSemanalRetirada
{
    public Guid PlantillaId { get; private set; }

    private PlantillaSemanalRetirada(Guid plantillaId) => PlantillaId = plantillaId;

    // Constructor vacio privado para Marten/JSON (mismo patron que TurnoRetirado).
    private PlantillaSemanalRetirada() { }

    public static PlantillaSemanalRetirada Crear(Guid plantillaId) => new(plantillaId);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(PlantillaSemanalRetirada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(PlantillaSemanalRetirada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (PlantillaSemanalRetirada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(PlantillaSemanalRetirada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
