namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

// Issue #223 CA-2: formas minimas planas para filtrar dead letters por el identificador de
// correlacion de esta corrida, sin depender de la deserializacion custom de los value objects
// ricos de los contratos (ADR-0025). Los payloads viajan planos por el bus, asi que el
// serializador por defecto basta para deserializar solo el identificador.
public sealed record ProgramacionTurnoDiarioSolicitadaMinimo(Guid SolicitudId);

public sealed record DiaCalculadoMinimo(InformacionEmpleadoMinimo? InformacionEmpleado);

public sealed record InformacionEmpleadoMinimo(string EmpleadoId);
