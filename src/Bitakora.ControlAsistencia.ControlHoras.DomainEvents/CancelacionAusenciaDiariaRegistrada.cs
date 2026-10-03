using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public sealed class CancelacionAusenciaDiariaRegistrada
{
    public string Id { get; private set; } = null!;
    public Guid AusenciaId { get; private set; }
    public DateOnly Fecha { get; private set; }

    private CancelacionAusenciaDiariaRegistrada(string id, Guid ausenciaId, DateOnly fecha)
    {
        Id = id;
        AusenciaId = ausenciaId;
        Fecha = fecha;
    }

    private CancelacionAusenciaDiariaRegistrada() { }

    public static CancelacionAusenciaDiariaRegistrada Crear(string id, Guid ausenciaId, DateOnly fecha) =>
        new(id, ausenciaId, fecha);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(CancelacionAusenciaDiariaRegistrada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(CancelacionAusenciaDiariaRegistrada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (CancelacionAusenciaDiariaRegistrada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(CancelacionAusenciaDiariaRegistrada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
