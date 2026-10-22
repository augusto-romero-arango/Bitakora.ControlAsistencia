using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class CoberturaVecinaActualizada
{
    public string Id { get; private set; } = null!;
    public DiaVecino Vecino { get; private set; }
    public DateTime? Limite { get; private set; }

    private CoberturaVecinaActualizada(string id, DiaVecino vecino, DateTime? limite)
    {
        Id = id;
        Vecino = vecino;
        Limite = limite;
    }

    private CoberturaVecinaActualizada() { }

    public static CoberturaVecinaActualizada Crear(string id, DiaVecino vecino, DateTime? limite) =>
        new(id, vecino, limite);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(CoberturaVecinaActualizada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(CoberturaVecinaActualizada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (CoberturaVecinaActualizada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(CoberturaVecinaActualizada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
