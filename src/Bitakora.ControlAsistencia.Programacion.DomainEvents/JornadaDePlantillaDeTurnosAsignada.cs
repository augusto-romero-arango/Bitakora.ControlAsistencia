using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class JornadaDePlantillaDeTurnosAsignada
{
    public Guid PlantillaId { get; private set; }
    public Guid JornadaId { get; private set; }
    public LimitesJornada Limites { get; private set; } = null!;
    public long VersionJornada { get; private set; }

    private JornadaDePlantillaDeTurnosAsignada() { }

    public static JornadaDePlantillaDeTurnosAsignada Crear(
        Guid plantillaId, Guid jornadaId, LimitesJornada limites, long versionJornada) =>
        throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
