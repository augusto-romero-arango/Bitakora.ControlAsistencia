namespace Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction;

// Comando interno: el endpoint lo compone integramente desde el {id} de la ruta, sin body
// (MEF-ADR-0043 paso 3 -- remocion veraz de un recurso direccionable).
public record RetirarPlantillaSemanal(Guid PlantillaId);
