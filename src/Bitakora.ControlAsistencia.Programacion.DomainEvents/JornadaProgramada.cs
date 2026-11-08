namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public record JornadaProgramada(
    Guid JornadaId,
    LimitesJornada Limites,
    ModalidadJornada? Modalidad = null,
    bool DescansoAcumulado = false,
    FichaDeLey? Ficha = null);
