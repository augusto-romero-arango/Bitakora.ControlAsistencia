using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class JornadaDePlantillaDeTurnosQuitada
{
    public Guid PlantillaId { get; private set; }

    private JornadaDePlantillaDeTurnosQuitada() { }

    public static JornadaDePlantillaDeTurnosQuitada Crear(Guid plantillaId) =>
        throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
