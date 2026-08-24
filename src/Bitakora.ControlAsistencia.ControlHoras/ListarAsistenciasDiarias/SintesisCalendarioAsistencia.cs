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
        // Un documento fuera de [desde, hastaAplicado] se ignora (contrato de la clase): el
        // filtrado por rango ya deberia haber ocurrido en la consulta LINQ del endpoint, esto es
        // solo una salvaguarda de la funcion pura.
        var documentoPorFecha = documentos
            .Where(d => d.Fecha >= desde && d.Fecha <= hastaAplicado)
            .ToDictionary(d => d.Fecha);

        var filas = new List<FilaAsistenciaDiaria>();
        for (var fecha = desde; fecha <= hastaAplicado; fecha = fecha.AddDays(1))
        {
            filas.Add(documentoPorFecha.TryGetValue(fecha, out var documento)
                ? MapearConDocumento(documento)
                : FilaSintetica(fecha));
        }

        return filas;
    }

    private static FilaAsistenciaDiaria MapearConDocumento(AsistenciaDiaria documento) =>
        new(
            documento.Fecha,
            MapearEstado(documento.Estado),
            documento.Plan,
            documento.NombreTurno,
            documento.NoSePresento,
            documento.FranjasIncompletas,
            documento.VinoEnDescanso,
            documento.TrabajoSinProgramacion,
            documento.HorasPorConcepto);

    // CA-2: dia sin documento -- decision A del Aprobador, "no vino y no debia venir" se avala, no
    // se aprueba.
    private static FilaAsistenciaDiaria FilaSintetica(DateOnly fecha) =>
        new(
            fecha,
            EstadoAsistenciaPresentado.SinDatos,
            PlanDelDia.SinProgramar,
            null,
            false,
            false,
            false,
            false,
            new Dictionary<string, decimal>());

    private static EstadoAsistenciaPresentado MapearEstado(EstadoAsistencia estado) => estado switch
    {
        EstadoAsistencia.Provisional => EstadoAsistenciaPresentado.Provisional,
        EstadoAsistencia.Aprobado => EstadoAsistenciaPresentado.Aprobado,
        _ => throw new ArgumentOutOfRangeException(
            nameof(estado), estado, "EstadoAsistencia no reconocido por el mapeo de estado presentado")
    };
}
