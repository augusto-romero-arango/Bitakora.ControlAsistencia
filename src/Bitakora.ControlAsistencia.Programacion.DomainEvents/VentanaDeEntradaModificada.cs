using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class VentanaDeEntradaModificada
{
    public Guid TurnoId { get; private set; }
    public HorasYMinutos Amplitud { get; private set; } = null!;

    private VentanaDeEntradaModificada() { }

    private VentanaDeEntradaModificada(Guid turnoId, HorasYMinutos amplitud)
    {
        TurnoId = turnoId;
        Amplitud = amplitud;
    }

    public static VentanaDeEntradaModificada Crear(Guid turnoId, HorasYMinutos amplitud) => new(turnoId, amplitud);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(VentanaDeEntradaModificada);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (VentanaDeEntradaModificada)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
