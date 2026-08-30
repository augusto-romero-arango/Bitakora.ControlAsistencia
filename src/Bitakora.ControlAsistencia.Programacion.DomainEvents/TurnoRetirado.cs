using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Retiro de un turno del catalogo: ya no es asignable a nuevas solicitudes. No afecta los dias
// ya programados -- la solicitud copia su propio snapshot via CatalogoTurnos.ObtenerDetalle().
public sealed class TurnoRetirado
{
    public Guid TurnoId { get; private set; }

    private TurnoRetirado(Guid turnoId) => TurnoId = turnoId;

    // Constructor vacio privado para Marten/JSON (mismo patron que TurnoCreado).
    private TurnoRetirado() { }

    public static TurnoRetirado Crear(Guid turnoId) => new(turnoId);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(TurnoRetirado)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(TurnoRetirado)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (TurnoRetirado)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(TurnoRetirado).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
