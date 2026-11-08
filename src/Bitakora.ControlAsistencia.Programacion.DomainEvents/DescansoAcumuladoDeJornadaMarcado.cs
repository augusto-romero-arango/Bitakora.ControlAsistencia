namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class DescansoAcumuladoDeJornadaMarcado
{
    public Guid JornadaId { get; private set; }

    private DescansoAcumuladoDeJornadaMarcado() { }
    private DescansoAcumuladoDeJornadaMarcado(Guid jornadaId) => JornadaId = jornadaId;

    public static DescansoAcumuladoDeJornadaMarcado Crear(Guid jornadaId) => new(jornadaId);
}
