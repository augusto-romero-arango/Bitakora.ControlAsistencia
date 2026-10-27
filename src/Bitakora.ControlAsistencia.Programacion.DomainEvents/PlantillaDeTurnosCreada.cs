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

    public static PlantillaDeTurnosCreada Crear(Guid plantillaId, string nombre, int diasDelCiclo)
        => throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
    }
}
