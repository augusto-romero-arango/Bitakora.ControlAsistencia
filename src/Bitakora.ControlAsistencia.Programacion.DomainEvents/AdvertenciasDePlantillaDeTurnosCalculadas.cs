using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class AdvertenciasDePlantillaDeTurnosCalculadas
{
    public Guid PlantillaId { get; private set; }
    public IReadOnlyList<AdvertenciaPlantillaTurnos> Advertencias { get; private set; } = [];

    private AdvertenciasDePlantillaDeTurnosCalculadas() { }

    public static AdvertenciasDePlantillaDeTurnosCalculadas Crear(
        Guid plantillaId, IReadOnlyList<AdvertenciaPlantillaTurnos> advertencias) =>
        throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
    }
}
