using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class JornadaDePlantillaDeTurnosAsignada
{
    public Guid PlantillaId { get; private set; }
    public Guid JornadaId { get; private set; }

    private JornadaDePlantillaDeTurnosAsignada() { }

    private JornadaDePlantillaDeTurnosAsignada(Guid plantillaId, Guid jornadaId)
    {
        PlantillaId = plantillaId;
        JornadaId = jornadaId;
    }

    public static JornadaDePlantillaDeTurnosAsignada Crear(Guid plantillaId, Guid jornadaId) =>
        new(plantillaId, jornadaId);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(JornadaDePlantillaDeTurnosAsignada);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (JornadaDePlantillaDeTurnosAsignada)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
