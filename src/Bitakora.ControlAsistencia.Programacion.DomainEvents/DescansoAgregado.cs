using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class DescansoAgregado
{
    public Guid TurnoId { get; private set; }
    public FranjaOrdinaria Franja { get; private set; } = null!;

    private DescansoAgregado(Guid turnoId, FranjaOrdinaria franja)
    {
        TurnoId = turnoId;
        Franja = franja;
    }

    // Constructor vacio privado para Marten/JSON.
    private DescansoAgregado() { }

    public static DescansoAgregado Crear(Guid turnoId, FranjaOrdinaria franja) => new(turnoId, franja);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(DescansoAgregado)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(DescansoAgregado)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (DescansoAgregado)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(DescansoAgregado).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
