using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #622: vacia el slot (semana, dia) de la plantilla. Su resultado es una ausencia
// (CA-ADR-0033 decision 4): sin TurnoId -- la clave (Semana, Dia) ya localiza el slot que Apply
// debe vaciar, sin necesitar el turno que se fue. Mismo patron que DiaDePlantillaSemanalAsignado:
// sealed class con ctor privado + ctor vacio para Marten/JSON.
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

    // Constructor vacio privado para Marten/JSON (mismo patron que DiaDePlantillaSemanalAsignado).
    private DiaDePlantillaSemanalQuitado() => Dia = DiaSemana.Lunes;

    public static DiaDePlantillaSemanalQuitado Crear(Guid plantillaId, int semana, DiaSemana dia)
    {
        if (semana < 1)
            throw new ArgumentException(Mensajes.SemanaNoPositiva, nameof(semana));

        return new DiaDePlantillaSemanalQuitado(plantillaId, semana, dia);
    }

    // Dia persiste como su numero ISO (entero), mismo mecanismo que DiaDePlantillaSemanalAsignado.
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
