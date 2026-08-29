using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Sedes;

// Issue #467: resultado del enriquecimiento coreografiado (MEF-ADR-0046, paso 3) que Sedes publica
// tras resolver la ubicacion de una marcacion. Consumido por ControlHoras en #463 (estampado sobre
// ControlDiario), via el topic nuevo "sede-de-marcacion-resuelta".
//
// Payload plano de seis campos top-level, sin DTO de correlacion anidado (refinamiento del experto,
// 2026-08-29): los tres primeros son la terna de correlacion tal cual llego en RegistroDeMarcacionCreado
// (la marcacion no tiene id propio); los tres ultimos son el estampado autocontenido -- nombre y CC
// VIGENTES al momento de resolver, porque una modificacion futura al maestro de sedes nunca debe
// cambiar un hecho ya resuelto (MEF-ADR-0039 decision 6: el evento no importa ningun tipo de
// ControlHoras ni de PublicEvents/DomainEvents, solo primitivos).
//
// NombreSede (no "Nombre"): en un payload plano de 6 campos un "Nombre" suelto es ambiguo entre el
// nombre de la sede o el del colaborador.
//
// Nunca se persiste: no entra en ConfiguracionSerializacionSedes ni en IdentidadEventosSedes.TiposPersistidos
// (no existe tal evento de dominio -- MEF-ADR-0046 seccion 3 paso 2/3: el dueno solo consulta su
// read model y publica, sin abrir stream propio). Vive solo en el canal.
public record SedeDeMarcacionResuelta(
    string CodigoColaborador,
    DateTime TimestampNormalizado,
    string DispositivoId,
    string CodigoSede,
    string NombreSede,
    string? CentroDeCostos) : IPrivateEvent;
