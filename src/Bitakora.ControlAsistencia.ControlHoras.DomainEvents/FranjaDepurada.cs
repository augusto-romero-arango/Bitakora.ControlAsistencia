namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Espejo de PrivateEvents.ControlHoras.FranjaDepurada: tipo distinto porque ningun ensamblado de
// eventos referencia a otro (CA-ADR-0029, tres islas).
//
// La sede es la PROGRAMADA (el plan); confrontarla con la marcada es juicio del expediente, nunca
// de este payload. Sus 3 campos son opcionales porque el cambio fue ADITIVO: los streams escritos
// antes deserializan con null, sin protocolo de dos despliegues (MEF-ADR-0036).
public record FranjaDepurada(
    TimeOnly HoraInicioProgramada,
    TimeOnly HoraFinProgramada,
    int DiaOffsetFin,
    DateTime? Entrada,
    DateTime? Salida,
    bool EsAnomala,
    string? CodigoSedeProgramada = null,
    string? NombreSedeProgramada = null,
    string? CentroDeCostosProgramado = null);
