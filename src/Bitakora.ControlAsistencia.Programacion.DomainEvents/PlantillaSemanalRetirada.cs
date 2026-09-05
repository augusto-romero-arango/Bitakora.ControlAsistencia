using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Retiro de una plantilla semanal: deja de ser usable (CA-ADR-0034 decision 4, espejo de
// TurnoRetirado). El stream conserva la memoria -- nada se borra del event store.
public sealed class PlantillaSemanalRetirada
{
    public Guid PlantillaId { get; private set; }

    private PlantillaSemanalRetirada(Guid plantillaId) => PlantillaId = plantillaId;

    // Constructor vacio privado para Marten/JSON (mismo patron que TurnoRetirado).
    private PlantillaSemanalRetirada() { }

    public static PlantillaSemanalRetirada Crear(Guid plantillaId) => throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
