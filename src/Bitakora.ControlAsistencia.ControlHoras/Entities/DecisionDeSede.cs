namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Insumo que el acto de aprobar exige solo donde la maquina se abstuvo: por franja en conflicto, el
// codigo de sede que el Aprobador eligio. Vive en Entities y no dentro del comando para que el
// aggregate no dependa de la forma del trigger HTTP -- misma direccion (borde -> nucleo) que
// MEF-ADR-0039 decision 6 fija para el factory de un evento.
// Nombre y centro de costos NO viajan aqui: DiaCalculadoAggregateRoot los resuelve contra sus
// propias candidatas (Tell-don't-Ask, MEF-ADR-0012).
public sealed record DecisionDeSede(TimeOnly HoraInicioProgramada, string CodigoSede);
