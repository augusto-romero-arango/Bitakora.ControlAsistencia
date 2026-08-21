using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Nunca se persiste: IdentidadEventosControlHoras lo excluye a proposito. El nombre DiaCalculado
// queda reservado al aggregate del flujo de aprobacion (#425), que consume este evento.
//
// CodigoColaborador viaja top-level y siempre presente, tambien cuando Colaborador es null (dia
// nacido solo por marcacion, sin turno): el consumidor arma "dc:{codigo}:{yyyyMMdd}" con el. Moverlo
// dentro de Colaborador reintroduce el defecto que este campo corrige.
public record DiaDepurado(
    string CodigoColaborador,
    DateOnly Fecha,
    ResumenColaborador? Colaborador,
    HorasDiscriminadas HorasDiscriminadas) : IPrivateEvent;
