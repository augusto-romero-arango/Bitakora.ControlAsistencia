using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class JornadaCreada
{
    public Guid JornadaId { get; private set; }
    public LimitesJornada Limites { get; private set; } = null!;

    private JornadaCreada() { }

    public static JornadaCreada Crear(Guid jornadaId, LimitesJornada limites) => throw new NotImplementedException();
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) => throw new NotImplementedException();
}
