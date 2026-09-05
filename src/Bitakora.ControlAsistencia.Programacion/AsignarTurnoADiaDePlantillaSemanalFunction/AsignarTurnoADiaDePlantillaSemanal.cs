using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction;

// Comando interno compuesto por el endpoint a partir de {id}/{semana}/{dia} (ruta, ya parseados y
// validados) + TurnoId (body reducido, ver AsignarTurnoADiaDePlantillaSemanalBody).
public record AsignarTurnoADiaDePlantillaSemanal(Guid PlantillaId, int Semana, DiaSemana Dia, Guid TurnoId);
