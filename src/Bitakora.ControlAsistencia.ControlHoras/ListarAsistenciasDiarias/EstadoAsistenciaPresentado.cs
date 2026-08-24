namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Estado presentado de una fila de la respuesta, nunca del read model AsistenciaDiaria: SinDatos
/// describe un dia sin documento -- fila sintetica que la proyeccion no materializa. Provisional y
/// Aprobado mapean 1:1 con EstadoAsistencia.
/// </summary>
public enum EstadoAsistenciaPresentado
{
    Provisional,
    Aprobado,
    SinDatos
}
