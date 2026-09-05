using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Se agrega un descanso a una franja ordinaria ya existente de un turno del catalogo
// (CA-ADR-0033, diseno de turno por pasos). No cruza ningun bus: solo se persiste en el event
// store de Programacion -- misma razon que FranjaAgregada (MEF-ADR-0012).
public sealed class DescansoAgregado
{
    public Guid TurnoId { get; private set; }
    public FranjaOrdinaria Franja { get; private set; } = null!;

    private DescansoAgregado(Guid turnoId, FranjaOrdinaria franja)
    {
        TurnoId = turnoId;
        Franja = franja;
    }

    // Constructor vacio privado para Marten/JSON.
    private DescansoAgregado() { }

    public static DescansoAgregado Crear(Guid turnoId, FranjaOrdinaria franja) => new(turnoId, franja);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
