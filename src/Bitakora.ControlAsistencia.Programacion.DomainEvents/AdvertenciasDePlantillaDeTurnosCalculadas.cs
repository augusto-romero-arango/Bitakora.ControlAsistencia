using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class AdvertenciasDePlantillaDeTurnosCalculadas
{
    public Guid PlantillaId { get; private set; }
    public IReadOnlyList<AdvertenciaPlantillaTurnos> Advertencias { get; private set; } = [];

    private AdvertenciasDePlantillaDeTurnosCalculadas() { }

    private AdvertenciasDePlantillaDeTurnosCalculadas(
        Guid plantillaId, IReadOnlyList<AdvertenciaPlantillaTurnos> advertencias)
    {
        PlantillaId = plantillaId;
        Advertencias = advertencias;
    }

    public static AdvertenciasDePlantillaDeTurnosCalculadas Crear(
        Guid plantillaId, IReadOnlyList<AdvertenciaPlantillaTurnos> advertencias) =>
        new(plantillaId, advertencias);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(AdvertenciasDePlantillaDeTurnosCalculadas);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (AdvertenciasDePlantillaDeTurnosCalculadas)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
