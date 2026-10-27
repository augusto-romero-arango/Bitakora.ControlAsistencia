using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class JornadaDePlantillaDeTurnosQuitada
{
    public Guid PlantillaId { get; private set; }

    private JornadaDePlantillaDeTurnosQuitada() { }

    private JornadaDePlantillaDeTurnosQuitada(Guid plantillaId) => PlantillaId = plantillaId;

    public static JornadaDePlantillaDeTurnosQuitada Crear(Guid plantillaId) => new(plantillaId);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(JornadaDePlantillaDeTurnosQuitada);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (JornadaDePlantillaDeTurnosQuitada)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
