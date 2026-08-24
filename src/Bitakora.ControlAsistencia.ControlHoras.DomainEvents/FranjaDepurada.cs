namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Issue #425: payload propio de esta isla, espejo de PrivateEvents.ControlHoras.FranjaDepurada
// (payload por rol, MEF-ADR-0039 decision #6) -- mismo termino del glosario, tipo distinto porque
// ningun ensamblado de eventos referencia a otro (CA-ADR-0029 decision #2, tres islas).
public record FranjaDepurada(
    TimeOnly HoraInicioProgramada,
    TimeOnly HoraFinProgramada,
    int DiaOffsetFin,
    DateTime? Entrada,
    DateTime? Salida,
    bool EsAnomala);
