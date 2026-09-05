namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction;

// Body reducido a { "turnoId": "..." }: PlantillaId, Semana y Dia viajan en la ruta (CA-ADR-0031
// seccion 1, componentes URL-safe descompuestos por segmento).
public record AsignarTurnoADiaDePlantillaSemanalBody(Guid TurnoId);
