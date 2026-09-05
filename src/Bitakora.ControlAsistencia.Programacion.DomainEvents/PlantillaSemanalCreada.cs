using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #620: evento que registra la creacion de una plantilla semanal de turnos vacia (segundo
// nivel de composicion sobre el Turno, CA-ADR-0034). Mismo patron que TurnoCreado/TurnoRetirado:
// sealed class con ctor privado + factory que acumula errores, ctor vacio privado para Marten/JSON.
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

    // Constructor vacio privado para Marten/JSON (mismo patron que TurnoCreado).
    private PlantillaSemanalCreada()
    {
        Nombre = string.Empty;
    }

    // El evento nunca se construye en estado invalido: debe acumular TODOS los errores antes de
    // lanzar AggregateException (mismo patron que TurnoCreado.Crear). El tope de 6 semanas es
    // decision del experto (2026-09-05).
    public static PlantillaSemanalCreada Crear(Guid plantillaId, string nombre, int semanas) =>
        throw new NotImplementedException();

    // Mapping de serializacion para STJ/Marten -- mismo patron que TurnoRetirado.ConfigurarSerializacion.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
