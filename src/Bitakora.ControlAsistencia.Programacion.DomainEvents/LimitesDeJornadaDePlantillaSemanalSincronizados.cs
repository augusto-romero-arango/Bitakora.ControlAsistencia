using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class LimitesDeJornadaDePlantillaSemanalSincronizados
{
    public Guid PlantillaId { get; private set; }
    public Guid JornadaId { get; private set; }
    public LimitesJornada Limites { get; private set; } = null!;
    public long VersionJornada { get; private set; }

    private LimitesDeJornadaDePlantillaSemanalSincronizados() { }

    public static LimitesDeJornadaDePlantillaSemanalSincronizados Crear(
        Guid plantillaId, Guid jornadaId, LimitesJornada limites, long versionJornada) =>
        new() { PlantillaId = plantillaId, JornadaId = jornadaId, Limites = limites, VersionJornada = versionJornada };

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(LimitesDeJornadaDePlantillaSemanalSincronizados);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (LimitesDeJornadaDePlantillaSemanalSincronizados)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
