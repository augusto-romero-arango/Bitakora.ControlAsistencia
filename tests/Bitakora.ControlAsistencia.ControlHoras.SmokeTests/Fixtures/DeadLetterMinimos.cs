namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

// Formas minimas para filtrar dead letters por el id de correlacion de esta corrida sin depender de
// la deserializacion custom de los value objects ricos: el payload viaja plano por el bus.

public sealed record DiaDepuradoMinimo(string CodigoColaborador);

public sealed record CancelacionTurnoDiarioSolicitadaMinimo(Guid SolicitudId);

public sealed record AusenciaDiariaProgramadaMinimo(Guid AusenciaId);

public sealed record AusenciaDiariaCanceladaMinimo(Guid AusenciaId);
