namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Estado presentado de una fila de ListarAsistenciasDiarias (issue #427). Vive en el DTO de
/// respuesta del endpoint, NUNCA en el read model AsistenciaDiaria (#426): SinDatos describe un
/// dia que no genero documento -- fila sintetica -- y el read model nunca materializa filas
/// sinteticas (issue #427, "Necesidad de lectura").
///
/// Provisional/Aprobado mapean 1:1 con EstadoAsistencia (ReadModels.ControlHoras). SinDatos no
/// tiene contraparte en el read model: es el estado real que decision A del Aprobador exige para
/// avalar un dia vacio ("no vino y no debia venir") -- cuando llegue el aval, probablemente un
/// cuarto valor (issue #427, "Contexto").
/// </summary>
public enum EstadoAsistenciaPresentado
{
    Provisional,
    Aprobado,
    SinDatos
}
