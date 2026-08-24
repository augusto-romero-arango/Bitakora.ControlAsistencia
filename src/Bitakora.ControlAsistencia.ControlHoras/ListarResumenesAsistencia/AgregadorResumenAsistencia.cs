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
        IReadOnlyList<AsistenciaDiaria> documentos) =>
        throw new NotImplementedException();
}
