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

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
