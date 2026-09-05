using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction;

// Comando interno: el endpoint lo compone integramente desde la ruta ({id}/{semana}/{dia}), sin
// body (MEF-ADR-0043 paso 3 -- remocion veraz de un sub-recurso direccionable).
public record QuitarTurnoDeDiaDePlantillaSemanal(Guid PlantillaId, int Semana, DiaSemana Dia);
