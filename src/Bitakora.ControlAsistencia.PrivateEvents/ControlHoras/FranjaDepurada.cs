namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Issue #424: payload plano de una franja ordinaria depurada, espejo de ControlFranja
// (ControlHoras.Entities) para el mundo humano -- plan (HoraInicioProgramada, HoraFinProgramada,
// DiaOffsetFin) + realidad (Entrada, Salida, EsAnomala). SIN sede, SIN descripcion, SIN
// descansos/extras internos (ya digeridos en HorasDiscriminadas). La sede quedo explicitamente fuera
// de la conversacion por ahora.
//
// Todos los campos son primitivos -> la igualdad por valor del record por defecto ya es correcta, sin
// Equals/GetHashCode propios (MEF-ADR-0012).
public record FranjaDepurada(
    TimeOnly HoraInicioProgramada,
    TimeOnly HoraFinProgramada,
    int DiaOffsetFin,
    DateTime? Entrada,
    DateTime? Salida,
    bool EsAnomala);
