using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Issue #421: reclasifica el antiguo DiaCalculado (IPublicEvent) como evento privado intra-BC. El
// consumidor real (flujo de aprobacion, #425) vive dentro del mismo bounded context; DiaCalculado
// queda liberado para el aggregate de #425 (familia lexica: Depuracion automatica vs Conciliacion
// humana). Nunca se persiste -- ver IdentidadEventosControlHoras, que lo excluye explicitamente.
//
// CodigoColaborador sube a top-level (antes solo viajaba anidado en InformacionColaborador, que
// podia ser null): corrige un defecto latente -- el consumidor necesita construir
// "dc:{codigo}:{yyyyMMdd}" siempre, incluso cuando el dia nace solo por marcacion sin turno.
public record DiaDepurado(
    string CodigoColaborador,
    DateOnly Fecha,
    ResumenColaborador? Colaborador,
    HorasDiscriminadas HorasDiscriminadas) : IPrivateEvent;
