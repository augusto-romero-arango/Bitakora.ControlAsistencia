namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Sin marca usada/descartada a proposito: el receptor la deriva comparando el Timestamp con la Entrada
// o la Salida de cada FranjaDepurada (la idempotencia por minuto del aggregate los hace unicos).
//
// Pertenencia (DelTurno, FueraDeCobertura, Ambigua o DeTurnoVecino) si viaja: exige la cobertura propia
// y la de los turnos vecinos, que solo conoce ControlDiario. En un dia sin jornada valida solo puede ser
// DeTurnoVecino; las demas van en null.
//
// La sede MARCADA viaja por marcacion, no por franja: entrada y salida pueden venir de sedes
// distintas. Null hasta que llega el estampado coreografiado, posterior al hecho crudo.
public record MarcacionDelDia(
    DateTime Timestamp,
    string? Tipo,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null,
    string? Pertenencia = null);
