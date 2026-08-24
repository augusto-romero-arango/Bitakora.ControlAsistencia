using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// Agregacion en query-time: dado el rango aplicado, los codigos pedidos (o descubiertos) y las
/// filas AsistenciaDiaria del rango, produce una fila <see cref="ResumenAsistencia"/> por
/// colaborador. Funcion pura, sin Marten.
///
/// El universo de filas lo decide <c>codigosPedidos</c>, y las dos ramas NO son intercambiables:
/// - Presente: una fila por CADA codigo de la lista, en el mismo orden -- incluida la sintetica
///   (todo SinDatos/ceros) de un codigo sin documentos en el rango.
/// - Null: el universo son los colaboradores con al menos una fila en el rango, ascendente por
///   CodigoColaborador. Sin lista no hay a quien sintetizar: esta funcion no conoce la poblacion.
/// </summary>
public static class AgregadorResumenAsistencia
{
    public static IReadOnlyList<ResumenAsistencia> Agregar(
        DateOnly desde,
        DateOnly hastaAplicado,
        IReadOnlyList<string>? codigosPedidos,
        IReadOnlyList<AsistenciaDiaria> documentos)
    {
        // Re-filtrar por rango NO es redundante con el llamador: es el contrato defensivo de esta
        // funcion frente a uno que traiga documentos de mas. Sin el, esos documentos descuadrarian
        // los tres ejes contra los dias del rango.
        var documentosPorCodigo = documentos
            .Where(d => d.Fecha >= desde && d.Fecha <= hastaAplicado)
            .GroupBy(d => d.CodigoColaborador)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AsistenciaDiaria>)g.ToList());

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
        // Un dia sin documento es a la vez "sin programar" y SinDatos: se avala, no se aprueba. Un
        // documento con Plan.SinProgramar suma al primero pero no al segundo -- si existe, hubo
        // marcaciones que mirar.
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
