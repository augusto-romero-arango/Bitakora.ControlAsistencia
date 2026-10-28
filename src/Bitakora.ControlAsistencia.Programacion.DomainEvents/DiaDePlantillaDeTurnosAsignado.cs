using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class DiaDePlantillaDeTurnosAsignado
{
    public Guid PlantillaId { get; private set; }
    public int Dia { get; private set; }
    public Guid TurnoId { get; private set; }
    public Turno Turno { get; private set; } = Turno.Crear(string.Empty, false, []);
    public long VersionTurno { get; private set; }

    private DiaDePlantillaDeTurnosAsignado()
    {
    }

    private DiaDePlantillaDeTurnosAsignado(Guid plantillaId, int dia, Guid turnoId, Turno turno, long versionTurno)
    {
        PlantillaId = plantillaId;
        Dia = dia;
        TurnoId = turnoId;
        Turno = turno;
        VersionTurno = versionTurno;
    }

    public static DiaDePlantillaDeTurnosAsignado Crear(
        Guid plantillaId, int dia, Guid turnoId, Turno turno, long versionTurno)
    {
        if (dia < 1)
            throw new ArgumentException(Mensajes.DiaNoPositivo, nameof(dia));

        return new DiaDePlantillaDeTurnosAsignado(plantillaId, dia, turnoId, turno, versionTurno);
    }

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipoClase = typeof(DiaDePlantillaDeTurnosAsignado);
        var ctor = tipoClase.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != tipoClase) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = tipoClase.GetField(
                    $"<{prop.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
