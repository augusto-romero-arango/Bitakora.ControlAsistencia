namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Payload plano de una franja ordinaria depurada, espejo de ControlFranja (ControlHoras.Entities):
// plan + realidad. Deliberadamente sin descripcion y sin descansos/extras internos: esos ultimos ya
// vienen digeridos en HorasDiscriminadas y duplicarlos aqui daria dos fuentes de verdad.
//
// Issue #464: la sede PROGRAMADA (plan) viaja plana desde ControlFranja.Programada.Sede -- null
// cuando la franja no trae sede asignada (mismo criterio aditivo que FranjaProgramada.Sede, #336).
// Este issue solo transporta; el conflicto contra la sede marcada lo detecta y resuelve el
// expediente (#482), nunca ControlDiario.
//
// Solo primitivos: sumar un campo con coleccion obligaria a escribir Equals/GetHashCode a mano
// (MEF-ADR-0012, nota sobre equality).
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
