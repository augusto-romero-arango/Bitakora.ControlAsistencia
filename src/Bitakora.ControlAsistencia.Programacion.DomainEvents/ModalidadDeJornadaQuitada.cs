using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class ModalidadDeJornadaQuitada
{
    public Guid JornadaId { get; private set; }

    private ModalidadDeJornadaQuitada() { }
    private ModalidadDeJornadaQuitada(Guid jornadaId) => JornadaId = jornadaId;

    public static ModalidadDeJornadaQuitada Crear(Guid jornadaId) => new(jornadaId);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) => throw new NotImplementedException();
}
