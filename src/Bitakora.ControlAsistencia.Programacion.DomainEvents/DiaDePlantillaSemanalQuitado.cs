using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Su resultado es una ausencia (CA-ADR-0033 decision 4): sin TurnoId -- la clave (Semana, Dia) ya
// localiza el slot que Apply debe vaciar, sin necesitar el turno que se fue.
public sealed partial class DiaDePlantillaSemanalQuitado
{
    public Guid PlantillaId { get; private set; }
    public int Semana { get; private set; }
    public DiaSemana Dia { get; private set; }

    private DiaDePlantillaSemanalQuitado(Guid plantillaId, int semana, DiaSemana dia)
    {
        PlantillaId = plantillaId;
        Semana = semana;
        Dia = dia;
    }

    // Ctor vacio para Marten/JSON: sin el, ConfigurarSerializacion no tiene como instanciar el tipo.
    private DiaDePlantillaSemanalQuitado() => Dia = DiaSemana.Lunes;

    public static DiaDePlantillaSemanalQuitado Crear(Guid plantillaId, int semana, DiaSemana dia)
    {
        if (semana < 1)
            throw new ArgumentException(Mensajes.SemanaNoPositiva, nameof(semana));

        return new DiaDePlantillaSemanalQuitado(plantillaId, semana, dia);
    }

    // Contrato de persistencia: Dia se guarda como su numero ISO, nunca el nombre del enum de .NET.
    // STJ no sabe reconstruir DiaSemana (ctor privado), asi que se descarta su JsonPropertyInfo
    // auto-detectada y se sustituye por una de tipo int que rehidrata via DiaSemana.Desde.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipoClase = typeof(DiaDePlantillaSemanalQuitado);
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
            diaProp.Get = obj => ((DiaDePlantillaSemanalQuitado)obj).Dia.Numero;
            diaProp.Set = (obj, val) => diaBackingField.SetValue(obj, DiaSemana.Desde((int)val!));
            typeInfo.Properties.Add(diaProp);
        });
    }
}
