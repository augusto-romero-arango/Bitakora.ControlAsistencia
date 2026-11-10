namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Espejo plano de ControlFranja (plan + realidad). Sin descansos/extras: ya vienen digeridos en
// HorasDiscriminadas y duplicarlos daria dos fuentes de verdad.
//
// La sede es la PROGRAMADA, null si la franja no trae sede. Confrontarla con la sede marcada de cada
// MarcacionDelDia es un juicio del expediente, nunca de este payload.
//
// Solo primitivos: un campo con coleccion obligaria a escribir Equals/GetHashCode a mano.
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
