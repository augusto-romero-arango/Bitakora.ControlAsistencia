using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #500: evento que registra el retiro de un turno del catalogo -- ya no asignable a nuevas
// solicitudes. Los dias ya programados no dependen del catalogo (la solicitud copia su propio
// snapshot via CatalogoTurnos.ObtenerDetalle()), asi que este evento no los afecta.
public sealed class TurnoRetirado
{
    public Guid TurnoId { get; private set; }

    private TurnoRetirado(Guid turnoId) => TurnoId = turnoId;

    // CA-13 (mismo patron de TurnoCreado): constructor vacio privado para Marten/JSON
    private TurnoRetirado() { }

    public static TurnoRetirado Crear(Guid turnoId) => new(turnoId);

    // Mapping de serializacion para STJ/Marten -- mismo patron que TurnoCreado.ConfigurarSerializacion
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
