namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class DescansoAcumuladoDeJornadaDesmarcado
{
    public Guid JornadaId { get; private set; }

    private DescansoAcumuladoDeJornadaDesmarcado() { }
    private DescansoAcumuladoDeJornadaDesmarcado(Guid jornadaId) => JornadaId = jornadaId;

    public static DescansoAcumuladoDeJornadaDesmarcado Crear(Guid jornadaId) => new(jornadaId);
}
