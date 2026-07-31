using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Issue #270: contrato de bus que anuncia el nacimiento de un Registro de Marcacion
// (vocabulario: docs/eda/ubiquitous-language.yaml). Cruza fisicamente el ASB interno del BC hacia
// AdicionarMarcacion (mismo Bounded Context "Control de Asistencia") -> IPrivateEvent, no
// IPublicEvent (MEF-ADR-0024 decision #2).
//
// Payload plano y portable (MEF-ADR-0023, CA-ADR-0025): solo primitivos, cero esfuerzo de
// serializacion, sin resolver custom ni ConfigurarSerializacion. MEF-ADR-0012: "evento sin
// invariantes -> record con constructor primario, publico, sin validacion" -- sin colecciones en
// el payload, asi que la igualdad por valor que el record da gratis es honesta.
//
// Nombre simple distinto de MarcacionRegistrada (el evento de dominio persistido en el event store
// de ControlHoras.DomainEvents) a proposito: el Function App de ControlHoras referencia ambos
// ensamblados, y un using equivocado con el mismo nombre simple publicaria el evento rico al bus
// sin que el compilador lo detecte (CA-ADR-0025: el modelo de dominio rico no cruza el bus).
//
// Nunca se persiste: no entra en ConfiguracionSerializacionControlHoras ni en
// IdentidadEventosControlHoras.TiposPersistidos. Vive solo en el canal.
public record RegistroDeMarcacionCreado(
    string EmpleadoId,
    DateTime TimestampNormalizado,
    string? TipoMarcacion,
    string? DispositivoId) : IPrivateEvent;
