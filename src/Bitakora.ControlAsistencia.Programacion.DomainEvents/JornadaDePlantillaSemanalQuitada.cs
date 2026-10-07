namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class JornadaDePlantillaSemanalQuitada
{
    public Guid PlantillaId { get; private set; }

    private JornadaDePlantillaSemanalQuitada() { }

    private JornadaDePlantillaSemanalQuitada(Guid plantillaId) => PlantillaId = plantillaId;

    public static JornadaDePlantillaSemanalQuitada Crear(Guid plantillaId) => new(plantillaId);
}
