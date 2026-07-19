namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// Issue #223 CA-2: forma minima plana para filtrar dead letters de ProgramacionTurnoDiarioSolicitada
// por SolicitudId, sin depender de la deserializacion custom de los value objects ricos del
// contrato (ADR-0025). El payload viaja plano por el bus, asi que el serializador por defecto basta.
public sealed record ProgramacionTurnoDiarioSolicitadaMinimo(Guid SolicitudId);
