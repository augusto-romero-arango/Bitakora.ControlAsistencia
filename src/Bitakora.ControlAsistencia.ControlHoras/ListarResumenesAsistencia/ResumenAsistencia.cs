namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// DTO de respuesta -- excepcion justificada de MEF-ADR-0041 decision 4: no existe read model del
/// resumen (agregacion query-time sobre AsistenciaDiaria, #426) y la fila sintetica del codigo
/// pedido sin datos no existe en ninguna vista.
///
/// Los tres ejes del Aprobador (sesion 2026-08-17/2026-08-24), cada uno cerrando por separado
/// contra los dias del rango aplicado, sin mezclarse:
/// - Programacion: DiasConTurno + DiasConDescanso + DiasSinProgramar = dias del rango.
/// - Aprobacion (tres contadores, ratificados en el refinamiento del 2026-08-24): Aprobados +
///   Pendientes + SinDatos = dias del rango. Un dia vacio se AVALA (SinDatos), un provisional se
///   APRUEBA (Pendientes) -- colapsarlos descuadraria el drill-down de #427.
/// - Anomalias: conteo de cada una de las 4 banderas de AsistenciaDiaria; los dias sin fila no
///   aportan (no vino y no debia venir).
///
/// TotalHorasPorConcepto es la suma sparse (union de claves) de HorasPorConcepto de las filas del
/// colaborador en el rango.
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
    int Aprobados,
    int Pendientes,
    int SinDatos,
    IReadOnlyDictionary<string, decimal> TotalHorasPorConcepto);
