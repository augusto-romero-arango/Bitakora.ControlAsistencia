using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class ModalidadDeJornadaAsignada
{
    public Guid JornadaId { get; private set; }
    public ModalidadJornada Modalidad { get; private set; } = null!;

    private ModalidadDeJornadaAsignada() { }
    private ModalidadDeJornadaAsignada(Guid jornadaId, ModalidadJornada modalidad)
    {
        JornadaId = jornadaId;
        Modalidad = modalidad;
    }

    public static ModalidadDeJornadaAsignada Crear(Guid jornadaId, ModalidadJornada modalidad) => new(jornadaId, modalidad);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) => throw new NotImplementedException();
}
