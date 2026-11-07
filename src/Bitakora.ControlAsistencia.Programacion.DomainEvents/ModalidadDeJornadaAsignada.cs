using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class ModalidadDeJornadaAsignada
{
    public Guid JornadaId { get; private set; }
    public ModalidadJornada Modalidad { get; private set; } = null!;

    private ModalidadDeJornadaAsignada() { }
    private ModalidadDeJornadaAsignada(Guid jornadaId, ModalidadJornada modalidad)
    {
        JornadaId = jornadaId;
        Modalidad = modalidad;
    }

    public static ModalidadDeJornadaAsignada Crear(Guid jornadaId, ModalidadJornada modalidad) => new(jornadaId, modalidad);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(ModalidadDeJornadaAsignada);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (ModalidadDeJornadaAsignada)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
