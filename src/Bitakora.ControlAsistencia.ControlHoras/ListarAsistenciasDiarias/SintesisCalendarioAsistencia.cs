using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Produce exactamente una fila por cada dia del rango [desde, hastaAplicado], en orden ascendente
/// de fecha: el documento mapeado cuando ese dia lo tiene, o una fila sintetica cuando no. Funcion
/// pura -- el filtrado por colaborador ya ocurrio en la consulta del endpoint.
/// </summary>
public static class SintesisCalendarioAsistencia
{
    public static IReadOnlyList<FilaAsistenciaDiaria> Completar(
        DateOnly desde,
        DateOnly hastaAplicado,
        IReadOnlyList<AsistenciaDiaria> documentos)
    {
        // Re-filtrar por rango no es redundante con el endpoint: es el contrato de esta funcion
        // frente a un llamador que traiga documentos de mas.
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

    // Un dia sin documento no es una anomalia: no hubo programacion NI marcaciones, asi que las
    // cuatro banderas van en false -- el vacio se avala, no se aprueba.
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
