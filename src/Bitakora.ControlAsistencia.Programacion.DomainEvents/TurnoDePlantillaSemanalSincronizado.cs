using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class TurnoDePlantillaSemanalSincronizado
{
    public Guid PlantillaId { get; private set; }
    public Guid TurnoId { get; private set; }
    public Turno Turno { get; private set; } = null!;
    public long VersionTurno { get; private set; }
    public bool Retirado { get; private set; }

    private TurnoDePlantillaSemanalSincronizado() { }

    public static TurnoDePlantillaSemanalSincronizado Crear(
        Guid plantillaId, Guid turnoId, Turno turno, long versionTurno, bool retirado) =>
        new()
        {
            PlantillaId = plantillaId,
            TurnoId = turnoId,
            Turno = turno,
            VersionTurno = versionTurno,
            Retirado = retirado
        };

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(TurnoDePlantillaSemanalSincronizado);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (TurnoDePlantillaSemanalSincronizado)ctor.Invoke(null);
            foreach (var propiedad in info.Properties)
            {
                var campo = tipo.GetField($"<{propiedad.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
            }
        });
    }
}
