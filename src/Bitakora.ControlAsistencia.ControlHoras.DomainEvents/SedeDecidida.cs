namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Sede que el Aprobador eligio para una franja en conflicto. Nombre y CentroDeCostos vienen del
// estampado de la candidata elegida, nunca de un lookup al maestro de sedes ni del comando, que
// solo trae el codigo.
public sealed record SedeDecidida(
    TimeOnly HoraInicioProgramada,
    string CodigoSede,
    string? NombreSede,
    string? CentroDeCostos);
