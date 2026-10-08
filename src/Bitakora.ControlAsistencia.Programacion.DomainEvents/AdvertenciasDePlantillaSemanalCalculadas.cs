using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class AdvertenciasDePlantillaSemanalCalculadas
{
    public Guid PlantillaId { get; private set; }
    public IReadOnlyList<AdvertenciaPlantillaSemanal> Advertencias { get; private set; } = [];

    private AdvertenciasDePlantillaSemanalCalculadas() { }

    private AdvertenciasDePlantillaSemanalCalculadas(
        Guid plantillaId, IReadOnlyList<AdvertenciaPlantillaSemanal> advertencias)
    {
        PlantillaId = plantillaId;
        Advertencias = advertencias;
    }

    public static AdvertenciasDePlantillaSemanalCalculadas Crear(
        Guid plantillaId, IReadOnlyList<AdvertenciaPlantillaSemanal> advertencias) =>
        new(plantillaId, advertencias);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(AdvertenciasDePlantillaSemanalCalculadas);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (AdvertenciasDePlantillaSemanalCalculadas)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
