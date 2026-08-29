namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Payload propio de esta isla, espejo de PrivateEvents.ControlHoras.FranjaDepurada
// (payload por rol, MEF-ADR-0039 decision #6) -- mismo termino del glosario, tipo distinto porque
// ningun ensamblado de eventos referencia a otro (CA-ADR-0029 decision #2, tres islas).
//
// Issue #484: la sede que viaja aqui es la PROGRAMADA (el plan). Confrontarla contra la sede
// marcada de cada MarcacionDelDia es juicio del expediente (#482), nunca de este payload.
// Los 3 campos son opcionales por defecto porque el cambio es ADITIVO sobre un evento ya
// persistido: los streams escritos antes de #484 deserializan con null, sin protocolo de dos
// despliegues (MEF-ADR-0036).
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
