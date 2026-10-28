using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class PlantillaDeTurnosCreada
{
    public const int MaximoDiasDelCiclo = 42;

    public Guid PlantillaId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public int DiasDelCiclo { get; private set; }

    private PlantillaDeTurnosCreada()
    {
    }

    private PlantillaDeTurnosCreada(Guid plantillaId, string nombre, int diasDelCiclo)
    {
        PlantillaId = plantillaId;
        Nombre = nombre;
        DiasDelCiclo = diasDelCiclo;
    }

    public static PlantillaDeTurnosCreada Crear(Guid plantillaId, string nombre, int diasDelCiclo)
    {
        var errores = new List<Exception>();

        if (string.IsNullOrWhiteSpace(nombre))
            errores.Add(new ArgumentException(Mensajes.NombreVacio));

        if (diasDelCiclo is < 1 or > MaximoDiasDelCiclo)
            errores.Add(new ArgumentException(Mensajes.DiasDelCicloFueraDeRango));

        if (errores.Count > 0)
            throw new AggregateException(errores);

        return new PlantillaDeTurnosCreada(plantillaId, nombre, diasDelCiclo);
    }

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(PlantillaDeTurnosCreada)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(PlantillaDeTurnosCreada)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (PlantillaDeTurnosCreada)ctor.Invoke(null);

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(PlantillaDeTurnosCreada).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }
}
