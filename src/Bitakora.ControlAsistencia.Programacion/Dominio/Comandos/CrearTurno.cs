namespace Bitakora.ControlAsistencia.Programacion.Dominio.Comandos;

// Issue #3: comando DTO para crear un turno de trabajo
// ADR-0015: record = DTO sin invariantes, constructor primario publico
public record CrearTurno(
    Guid TurnoId,
    string Nombre,
    List<CrearTurno.Franja> Ordinarias)
{
    // CA-1: record anidado con las sub-franjas del turno
    public record Franja(
        TimeOnly Inicio,
        TimeOnly Fin,
        List<(TimeOnly inicio, TimeOnly fin)> Descansos,
        List<(TimeOnly inicio, TimeOnly fin)> Extras);
}
