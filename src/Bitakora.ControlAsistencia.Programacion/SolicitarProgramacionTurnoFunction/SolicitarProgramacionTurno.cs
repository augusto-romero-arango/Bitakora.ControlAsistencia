namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

public record SolicitarProgramacionTurno(
    Guid Id,
    Guid TurnoId,
    SolicitarProgramacionTurno.DatosEmpleado Empleado,
    List<DateOnly> Fechas)
{
    public record DatosEmpleado(
        string EmpleadoId,
        string TipoIdentificacion,
        string NumeroIdentificacion,
        string Nombres,
        string Apellidos);
}
