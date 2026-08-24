using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Sintesis pura del calendario completo de ListarAsistenciasDiarias (issue #427, CA-1/CA-2/CA-3/
/// CA-5). Dado el rango aplicado [desde, hastaAplicado] (ya recortado por
/// <see cref="RangoConsulta.Recortar"/>) y los documentos AsistenciaDiaria que la consulta LINQ del
/// endpoint trajo para ese colaborador y ese rango, produce EXACTAMENTE una fila por dia, orden
/// Fecha ascendente:
///
/// - Dia con documento: mapea sus campos (Plan, NombreTurno, las cuatro anomalias,
///   HorasPorConcepto) 1:1, con Estado presentado segun EstadoAsistencia -> EstadoAsistenciaPresentado
///   (Provisional/Aprobado).
/// - Dia sin documento: fila sintetica -- Estado = SinDatos, Plan = SinProgramar, NombreTurno =
///   null, las cuatro anomalias en false, HorasPorConcepto vacio (decision A: el vacio se avala, no
///   se aprueba).
///
/// Funcion pura sin QuerySession/Marten (skills/projections/read-apis.md): el filtrado por
/// CodigoColaborador y por rango ya ocurrio en la consulta LINQ del endpoint -- esta funcion solo
/// decide, para cada fecha del rango [desde, hastaAplicado], si existe un documento con esa Fecha;
/// un documento cuya Fecha caiga fuera del rango se ignora.
/// </summary>
public static class SintesisCalendarioAsistencia
{
    public static IReadOnlyList<FilaAsistenciaDiaria> Completar(
        DateOnly desde,
        DateOnly hastaAplicado,
        IReadOnlyList<AsistenciaDiaria> documentos)
    {
        throw new NotImplementedException();
    }
}
