using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction;

// Comando interno compuesto por el endpoint a partir de la ruta ({id}/{semana}/{dia}, ya parseados
// y validados) mas el TurnoId del body.
public record AsignarTurnoADiaDePlantillaSemanal(Guid PlantillaId, int Semana, DiaSemana Dia, Guid TurnoId);
