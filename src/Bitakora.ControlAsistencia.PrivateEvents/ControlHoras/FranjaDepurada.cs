namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Payload plano de una franja ordinaria depurada, espejo de ControlFranja (ControlHoras.Entities):
// plan + realidad. Deliberadamente sin sede, sin descripcion y sin descansos/extras internos: esos
// ultimos ya vienen digeridos en HorasDiscriminadas y duplicarlos aqui daria dos fuentes de verdad.
//
// Solo primitivos: sumar un campo con coleccion obligaria a escribir Equals/GetHashCode a mano
// (MEF-ADR-0012, nota sobre equality).
public record FranjaDepurada(
    TimeOnly HoraInicioProgramada,
    TimeOnly HoraFinProgramada,
    int DiaOffsetFin,
    DateTime? Entrada,
    DateTime? Salida,
    bool EsAnomala);
