using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #621: pone -- o reemplaza -- el turno de un dia de la plantilla semanal (CA-ADR-0034
// decisiones 1, 3 y 4). Referencia viva al turno (TurnoId), nunca copia: editar el turno despues
// se refleja solo. Mismo patron que PlantillaSemanalCreada: sealed class con ctor privado + ctor
// vacio para Marten/JSON.
public sealed partial class DiaDePlantillaSemanalAsignado
{
    public Guid PlantillaId { get; private set; }
    public int Semana { get; private set; }
    public DiaSemana Dia { get; private set; }
    public Guid TurnoId { get; private set; }

    private DiaDePlantillaSemanalAsignado(Guid plantillaId, int semana, DiaSemana dia, Guid turnoId)
    {
        PlantillaId = plantillaId;
        Semana = semana;
        Dia = dia;
        TurnoId = turnoId;
    }

    // Constructor vacio privado para Marten/JSON (mismo patron que PlantillaSemanalCreada).
    private DiaDePlantillaSemanalAsignado() => Dia = DiaSemana.Lunes;

    // El tope N de semanas es regla del aggregate (PlantillaSemanalTurnos.AsignarDia), no del
    // evento: aqui solo se valida el piso semana >= 1.
    public static DiaDePlantillaSemanalAsignado Crear(
        Guid plantillaId, int semana, DiaSemana dia, Guid turnoId)
    {
        if (semana < 1)
            throw new ArgumentException(Mensajes.SemanaNoPositiva, nameof(semana));

        return new DiaDePlantillaSemanalAsignado(plantillaId, semana, dia, turnoId);
    }

    // Dia persiste como su numero ISO (entero), nunca el nombre del enum de .NET ni una etiqueta
    // en espanol -- mismo mecanismo con que Identificacion.ConfigurarSerializacion persiste _tipo
    // como codigo literal y rehidrata via TipoIdentificacion.Desde.
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

            // PlantillaId, Semana, TurnoId: propiedades con private set de tipos que STJ ya
            // serializa nativamente (Guid, int) -- solo falta cablear el Set via el backing field
            // (mismo patron que PlantillaSemanalCreada). Dia se resuelve aparte, mas abajo.
            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                if (prop.Name == nameof(Dia)) continue;

                var backingField = tipoClase.GetField(
                    $"<{prop.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }

            var diaPropAutoDetectada = typeInfo.Properties.First(p => p.Name == nameof(Dia));
            typeInfo.Properties.Remove(diaPropAutoDetectada);

            var diaProp = typeInfo.CreateJsonPropertyInfo(typeof(int), nameof(Dia));
            diaProp.Get = obj => ((DiaDePlantillaSemanalAsignado)obj).Dia.Numero;
            diaProp.Set = (obj, val) => diaBackingField.SetValue(obj, DiaSemana.Desde((int)val!));
            typeInfo.Properties.Add(diaProp);
        });
    }
}
