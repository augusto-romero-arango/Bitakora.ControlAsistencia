using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class VentanaDeEntradaModificada
{
    public Guid TurnoId { get; private set; }
    public HorasYMinutos Amplitud { get; private set; } = null!;

    private VentanaDeEntradaModificada() { }

    public static VentanaDeEntradaModificada Crear(Guid turnoId, HorasYMinutos amplitud) =>
        throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) { }
}
