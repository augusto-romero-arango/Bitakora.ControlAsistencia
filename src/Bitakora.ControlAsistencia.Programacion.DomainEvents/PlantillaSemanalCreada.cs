using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Molde reutilizable de 1..N semanas lunes-domingo sobre el catalogo de turnos (CA-ADR-0034):
// nace vacia, sin dias -- los dias llegan por eventos de diseno posteriores.
public sealed partial class PlantillaSemanalCreada
{
    public const int MaximoSemanas = 6;

    public Guid PlantillaId { get; private set; }
    public string Nombre { get; private set; }
    public int Semanas { get; private set; }

    private PlantillaSemanalCreada(Guid plantillaId, string nombre, int semanas)
    {
        PlantillaId = plantillaId;
        Nombre = nombre;
        Semanas = semanas;
    }

    // Constructor vacio privado para Marten/JSON: sin el, ConfigurarSerializacion no tiene como
    // instanciar el tipo al deserializar.
    private PlantillaSemanalCreada()
    {
        Nombre = string.Empty;
    }

    // Acumula TODOS los errores antes de lanzar -- sin fail-fast: el 400 del endpoint devuelve la
    // lista completa de invariantes violadas.
    public static PlantillaSemanalCreada Crear(Guid plantillaId, string nombre, int semanas)
    {
        var errores = new List<Exception>();

        if (string.IsNullOrWhiteSpace(nombre))
            errores.Add(new ArgumentException(Mensajes.NombreVacio));

        if (semanas is < 1 or > MaximoSemanas)
            errores.Add(new ArgumentException(Mensajes.SemanasFueraDeRango));

        if (errores.Count > 0)
            throw new AggregateException(errores);

        return new PlantillaSemanalCreada(plantillaId, nombre, semanas);
    }

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(PlantillaSemanalCreada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(PlantillaSemanalCreada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (PlantillaSemanalCreada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(PlantillaSemanalCreada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
