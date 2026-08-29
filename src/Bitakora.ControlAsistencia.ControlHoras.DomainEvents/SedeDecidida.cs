namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Issue #489: candidata de sede que el Aprobador eligio para una franja en conflicto, resuelta por
// DiaCalculadoAggregateRoot.Aprobar contra sus propias candidatas internas. Nombre y CentroDeCostos
// vienen del estampado de la fuente elegida (MEF-ADR-0012, Tell-don't-Ask) -- nunca de un lookup al
// maestro de sedes ni del payload del comando, que solo trae el codigo.
public sealed record SedeDecidida(
    TimeOnly HoraInicioProgramada,
    string CodigoSede,
    string? NombreSede,
    string? CentroDeCostos);
