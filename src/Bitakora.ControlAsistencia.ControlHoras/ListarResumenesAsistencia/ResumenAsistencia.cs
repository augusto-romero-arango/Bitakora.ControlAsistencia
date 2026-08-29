namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// DTO de respuesta -- excepcion justificada de MEF-ADR-0041 decision 4: no existe read model del
/// resumen (agregacion query-time sobre AsistenciaDiaria) y la fila sintetica del codigo pedido sin
/// datos no existe en ninguna vista.
///
/// Los tres ejes del Aprobador no se mezclan y cada uno cierra por separado contra los dias del
/// rango aplicado:
/// - Programacion: DiasConTurno + DiasConDescanso + DiasSinProgramar = dias del rango.
/// - Aprobacion: Aprobados + Pendientes + SinDatos = dias del rango. Un dia vacio se AVALA
///   (SinDatos), un provisional se APRUEBA (Pendientes) -- colapsarlos en un solo contador
///   descuadraria el drill-down contra la pantalla de detalle, que presenta los tres estados.
/// - Anomalias: conteo de cada bandera de AsistenciaDiaria (incluida ConflictoDeSedePendiente,
///   issue #485); los dias sin fila no aportan ninguna (no vino y no debia venir), asi que este eje
///   NO cierra contra los dias del rango.
///
/// TotalHorasPorConcepto es la suma sparse (union de claves) de las filas del colaborador.
/// </summary>
public sealed record ResumenAsistencia(
    string CodigoColaborador,
    int DiasConTurno,
    int DiasConDescanso,
    int DiasSinProgramar,
    int NoSePresento,
    int FranjasIncompletas,
    int VinoEnDescanso,
    int TrabajoSinProgramacion,
    int ConflictoDeSedePendiente,
    int Aprobados,
    int Pendientes,
    int SinDatos,
    IReadOnlyDictionary<string, decimal> TotalHorasPorConcepto);
