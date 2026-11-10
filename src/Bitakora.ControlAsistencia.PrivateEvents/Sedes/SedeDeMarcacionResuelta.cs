using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Sedes;

// Sedes publica la ubicacion resuelta de una marcacion; ControlHoras la estampa.
//
// El consumidor lo deserializa con el serializador por defecto: ningun campo puede volverse un VO con
// ctor privado ni un tipo de otro ensamblado de eventos (CA-ADR-0025). CodigoColaborador +
// TimestampNormalizado + DispositivoId son la correlacion (la marcacion no tiene id propio); los otros
// tres son el estampado del momento del hecho, que un cambio posterior del maestro de sedes no altera.
//
// Nunca se persiste: no va en ConfiguracionSerializacionSedes ni en IdentidadEventosSedes.TiposPersistidos.
public record SedeDeMarcacionResuelta(
    string CodigoColaborador,
    DateTime TimestampNormalizado,
    string DispositivoId,
    string CodigoSede,
    string NombreSede,
    string? CentroDeCostos) : IPrivateEvent;
