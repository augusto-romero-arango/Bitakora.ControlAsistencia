using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// Agregacion en query-time (issue #428, "Necesidad de lectura", via a'): dado el rango aplicado,
/// los codigos pedidos (o descubiertos) y las filas AsistenciaDiaria del rango, produce una fila
/// ResumenAsistencia por colaborador. Funcion pura, sin Marten -- mismo patron que
/// SintesisCalendarioAsistencia.Completar (#427).
///
/// Contrato fijado por este test-writer (interfaz publica delegable del issue):
/// - CodigosColaborador presente (no null): una fila por CADA codigo de esa lista, en el mismo
///   orden -- incluida la fila sintetica (todo SinDatos/ceros) para un codigo sin documentos en el
///   rango.
/// - CodigosColaborador ausente (null): el universo es "colaboradores con &gt;= 1 fila en el
///   rango" -- se descubre de <paramref name="documentos"/> tras re-filtrar por rango (mismo
///   contrato defensivo que Completar frente a un llamador que traiga documentos de mas), en orden
///   ascendente de CodigoColaborador (mismo orden que exige la paginacion keyset del endpoint).
/// - HorasPorConcepto se suma sparse (union de claves) sobre los documentos del colaborador en el
///   rango.
/// </summary>
public static class AgregadorResumenAsistencia
{
    public static IReadOnlyList<ResumenAsistencia> Agregar(
        DateOnly desde,
        DateOnly hastaAplicado,
        IReadOnlyList<string>? codigosPedidos,
        IReadOnlyList<AsistenciaDiaria> documentos)
    {
        // Re-filtrar por rango no es redundante con el llamador: es el contrato defensivo de esta
        // funcion frente a un llamador que traiga documentos de mas (mismo patron que
        // SintesisCalendarioAsistencia.Completar, #427).
        var documentosPorCodigo = documentos
            .Where(d => d.Fecha >= desde && d.Fecha <= hastaAplicado)
            .GroupBy(d => d.CodigoColaborador)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AsistenciaDiaria>)g.ToList());

        // CodigosColaborador presente: una fila por cada codigo pedido, EN EL MISMO ORDEN de la
        // lista (incluida la sintetica de un codigo sin documentos). Ausente: universo descubierto
        // de los documentos, ordenado ascendente por CodigoColaborador -- mismo orden que exige la
        // paginacion keyset del endpoint.
        var codigos = codigosPedidos
            ?? documentosPorCodigo.Keys.OrderBy(codigo => codigo, StringComparer.Ordinal).ToList();

        var diasDelRango = hastaAplicado.DayNumber - desde.DayNumber + 1;

        return codigos
            .Select(codigo => MapearFila(
                codigo,
                diasDelRango,
                documentosPorCodigo.TryGetValue(codigo, out var documentosDelCodigo)
                    ? documentosDelCodigo
                    : []))
            .ToList();
    }

    private static ResumenAsistencia MapearFila(
        string codigo, int diasDelRango, IReadOnlyList<AsistenciaDiaria> documentos)
    {
        // Un dia sin fila (sin documento) es sin programacion Y SinDatos -- se avala, no se
        // aprueba (issue #428, "Necesidad de lectura"). Un documento con Plan.SinProgramar tambien
        // cuenta como "sin programar" en el eje programacion.
        var diasSinFila = diasDelRango - documentos.Count;

        var totalHorasPorConcepto = new Dictionary<string, decimal>();
        foreach (var documento in documentos)
            foreach (var (concepto, horas) in documento.HorasPorConcepto)
                totalHorasPorConcepto[concepto] =
                    totalHorasPorConcepto.GetValueOrDefault(concepto) + horas;

        return new ResumenAsistencia(
            codigo,
            documentos.Count(d => d.Plan == PlanDelDia.ConJornada),
            documentos.Count(d => d.Plan == PlanDelDia.Descanso),
            documentos.Count(d => d.Plan == PlanDelDia.SinProgramar) + diasSinFila,
            documentos.Count(d => d.NoSePresento),
            documentos.Count(d => d.FranjasIncompletas),
            documentos.Count(d => d.VinoEnDescanso),
            documentos.Count(d => d.TrabajoSinProgramacion),
            documentos.Count(d => d.Estado == EstadoAsistencia.Aprobado),
            documentos.Count(d => d.Estado == EstadoAsistencia.Provisional),
            diasSinFila,
            totalHorasPorConcepto);
    }
}
