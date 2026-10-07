using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class DiaDePlantillaSemanalAsignado
{
    public Guid PlantillaId { get; private set; }
    public int Semana { get; private set; }
    public DiaSemana Dia { get; private set; }
    public Guid TurnoId { get; private set; }
    public Turno Turno { get; private set; }
    public long VersionTurno { get; private set; }

    private DiaDePlantillaSemanalAsignado(
        Guid plantillaId, int semana, DiaSemana dia, Guid turnoId, Turno turno, long versionTurno)
    {
        PlantillaId = plantillaId;
        Semana = semana;
        Dia = dia;
        TurnoId = turnoId;
        Turno = turno;
        VersionTurno = versionTurno;
    }

    // Constructor vacio privado para Marten/JSON: sin el, ConfigurarSerializacion no tiene como
    // instanciar el tipo al deserializar.
    private DiaDePlantillaSemanalAsignado()
    {
        Dia = DiaSemana.Lunes;
        Turno = Turno.Crear(string.Empty, false, []);
    }

    // El tope N de semanas es regla del aggregate (PlantillaSemanalTurnos.AsignarDia), no del
    // evento: aqui solo se valida el piso.
    public static DiaDePlantillaSemanalAsignado Crear(
        Guid plantillaId, int semana, DiaSemana dia, Guid turnoId, Turno turno, long versionTurno)
    {
        if (semana < 1)
            throw new ArgumentException(Mensajes.SemanaNoPositiva, nameof(semana));

        return new DiaDePlantillaSemanalAsignado(plantillaId, semana, dia, turnoId, turno, versionTurno);
    }

    // Contrato de persistencia: Dia se guarda como su numero ISO (entero), nunca el nombre del enum
    // de .NET ni una etiqueta en espanol. STJ no sabe reconstruir DiaSemana (ctor privado), asi que
    // se descarta su JsonPropertyInfo auto-detectada y se sustituye por una de tipo int que
    // rehidrata via DiaSemana.Desde.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipoClase = typeof(DiaDePlantillaSemanalAsignado);
        var ctor = tipoClase.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        var diaBackingField = tipoClase.GetField(
            $"<{nameof(Dia)}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != tipoClase) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                if (prop.Name == nameof(Dia)) continue;

                var backingField = tipoClase.GetField(
                    $"<{prop.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }

            var diaAutoDetectada = typeInfo.Properties.First(p => p.Name == nameof(Dia));
            typeInfo.Properties.Remove(diaAutoDetectada);

            var diaProp = typeInfo.CreateJsonPropertyInfo(typeof(int), nameof(Dia));
            diaProp.Get = obj => ((DiaDePlantillaSemanalAsignado)obj).Dia.Numero;
            diaProp.Set = (obj, val) => diaBackingField.SetValue(obj, DiaSemana.Desde((int)val!));
            typeInfo.Properties.Add(diaProp);
        });
    }
}
