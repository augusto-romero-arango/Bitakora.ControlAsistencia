using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class SedeDeFranjaAsignada
{
    public Guid TurnoId { get; private set; }
    public FranjaOrdinaria Franja { get; private set; } = null!;

    private SedeDeFranjaAsignada(Guid turnoId, FranjaOrdinaria franja)
    {
        TurnoId = turnoId;
        Franja = franja;
    }

    // Constructor vacio privado para Marten/JSON.
    private SedeDeFranjaAsignada() { }

    public static SedeDeFranjaAsignada Crear(Guid turnoId, FranjaOrdinaria franja) => new(turnoId, franja);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(SedeDeFranjaAsignada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(SedeDeFranjaAsignada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (SedeDeFranjaAsignada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(SedeDeFranjaAsignada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
