namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Payload plano de una franja ordinaria depurada, espejo de ControlFranja (ControlHoras.Entities):
// plan + realidad. Deliberadamente sin descripcion y sin descansos/extras internos: esos ultimos ya
// vienen digeridos en HorasDiscriminadas y duplicarlos aqui daria dos fuentes de verdad.
//
// La sede que viaja aqui es la PROGRAMADA (el plan), plana y null cuando la franja no trae sede.
// ControlDiario solo la transporta: confrontarla contra la sede marcada de cada MarcacionDelDia es
// un juicio que pertenece al expediente (#482), nunca a este payload.
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
