namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Por franja en conflicto, el codigo de sede que el Aprobador eligio. Vive en Entities y no en el
// comando para que el aggregate no dependa de la forma del trigger HTTP. Nombre y centro de costos
// no viajan aqui: DiaCalculadoAggregateRoot los resuelve contra sus propias candidatas.
public sealed record DecisionDeSede(TimeOnly HoraInicioProgramada, string CodigoSede);
