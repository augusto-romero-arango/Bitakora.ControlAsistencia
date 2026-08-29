using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Sedes;

// Evento de bus del enriquecimiento coreografiado (MEF-ADR-0046 paso 3): Sedes publica la
// ubicacion resuelta de una marcacion; ControlHoras la estampa.
//
// Payload plano y portable: cruza el bus interno del BC y el consumidor lo deserializa con el
// serializador por defecto, sin resolver custom -- ningun campo puede volverse un VO con ctor
// privado ni un tipo de otro ensamblado de eventos (MEF-ADR-0039 decision 6, CA-ADR-0025).
// CodigoColaborador + TimestampNormalizado + DispositivoId son la correlacion (la marcacion no
// tiene id propio); los tres restantes son el estampado del momento del hecho, que una
// modificacion posterior del maestro de sedes no debe cambiar.
//
// Nunca se persiste: no va en ConfiguracionSerializacionSedes ni en
// IdentidadEventosSedes.TiposPersistidos -- la reaccion consulta el read model y publica, sin
// abrir stream propio.
public record SedeDeMarcacionResuelta(
    string CodigoColaborador,
    DateTime TimestampNormalizado,
    string DispositivoId,
    string CodigoSede,
    string NombreSede,
    string? CentroDeCostos) : IPrivateEvent;
