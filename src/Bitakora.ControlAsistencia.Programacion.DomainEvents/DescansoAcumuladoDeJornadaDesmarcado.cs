using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class DescansoAcumuladoDeJornadaDesmarcado
{
    public Guid JornadaId { get; private set; }

    private DescansoAcumuladoDeJornadaDesmarcado() { }
    private DescansoAcumuladoDeJornadaDesmarcado(Guid jornadaId) => JornadaId = jornadaId;

    public static DescansoAcumuladoDeJornadaDesmarcado Crear(Guid jornadaId) => new(jornadaId);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(DescansoAcumuladoDeJornadaDesmarcado);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (DescansoAcumuladoDeJornadaDesmarcado)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
