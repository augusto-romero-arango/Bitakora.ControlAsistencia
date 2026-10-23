using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class AmbiguedadDeMarcacionesResuelta
{
    public string Id { get; private set; } = null!;
    public DiaVecino Vecino { get; private set; }
    public IReadOnlyList<MarcacionDecidida> Marcaciones { get; private set; } = [];

    private AmbiguedadDeMarcacionesResuelta(string id, DiaVecino vecino, IReadOnlyList<MarcacionDecidida> marcaciones)
    {
        Id = id;
        Vecino = vecino;
        Marcaciones = marcaciones;
    }

    private AmbiguedadDeMarcacionesResuelta() { }

    public static AmbiguedadDeMarcacionesResuelta Crear(
        string id, DiaVecino vecino, IReadOnlyList<MarcacionDecidida> marcaciones) =>
        new(id, vecino, marcaciones);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(AmbiguedadDeMarcacionesResuelta)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(AmbiguedadDeMarcacionesResuelta)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (AmbiguedadDeMarcacionesResuelta)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(AmbiguedadDeMarcacionesResuelta).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
