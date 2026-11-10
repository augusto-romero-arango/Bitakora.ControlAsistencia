using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// El payload es la franja contenedora RESULTANTE, ya sin la hija: lo quitado se deduce comparando con
// el estado previo.
public sealed class DescansoQuitado
{
    public Guid TurnoId { get; private set; }
    public FranjaOrdinaria Franja { get; private set; } = null!;

    private DescansoQuitado(Guid turnoId, FranjaOrdinaria franja)
    {
        TurnoId = turnoId;
        Franja = franja;
    }

    // Constructor vacio privado para Marten/JSON.
    private DescansoQuitado() { }

    public static DescansoQuitado Crear(Guid turnoId, FranjaOrdinaria franja) => new(turnoId, franja);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(DescansoQuitado)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(DescansoQuitado)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (DescansoQuitado)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(DescansoQuitado).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
