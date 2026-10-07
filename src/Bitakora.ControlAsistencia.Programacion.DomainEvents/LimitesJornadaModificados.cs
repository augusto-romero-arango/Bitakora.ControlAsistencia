using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class LimitesJornadaModificados
{
    public Guid JornadaId { get; private set; }
    public LimitesJornada Limites { get; private set; } = null!;

    private LimitesJornadaModificados() { }
    private LimitesJornadaModificados(Guid jornadaId, LimitesJornada limites)
    {
        JornadaId = jornadaId;
        Limites = limites;
    }

    public static LimitesJornadaModificados Crear(Guid jornadaId, LimitesJornada limites) => new(jornadaId, limites);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(LimitesJornadaModificados);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (LimitesJornadaModificados)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
