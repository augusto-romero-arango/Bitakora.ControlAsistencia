using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// El payload es la franja COMPLETA que se fue (con descansos, extras y sede), no solo su hora de
// inicio: el stream es la unica memoria de lo que se quito.
public sealed class FranjaQuitada
{
    public Guid TurnoId { get; private set; }
    public FranjaOrdinaria Franja { get; private set; } = null!;

    private FranjaQuitada(Guid turnoId, FranjaOrdinaria franja)
    {
        TurnoId = turnoId;
        Franja = franja;
    }

    // Constructor vacio privado para Marten/JSON.
    private FranjaQuitada() { }

    public static FranjaQuitada Crear(Guid turnoId, FranjaOrdinaria franja) => new(turnoId, franja);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(FranjaQuitada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(FranjaQuitada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (FranjaQuitada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(FranjaQuitada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
