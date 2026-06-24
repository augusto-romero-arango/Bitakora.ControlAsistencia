namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #129: Estructura agregada del desglose del dia completo.
// Record con constructor primario publico - STJ lo serializa nativamente sin ConfigurarSerializacion (ADR-0015).
// FranjasAnomalas: franjas excluidas por falta de entrada o salida.
// RetardoTotal: consolidado del dia; lo calcula #116 con compensacion cruzada.
public record DesgloseHoras(
    IReadOnlyList<DesgloseFranja> DesglosePorFranja,
    DetalleRetardo RetardoTotal,
    int FranjasAnomalas)
{
    // CA-3: suma elemento a elemento de MinutosPorConcepto de cada DesgloseFranja.
    public IReadOnlyDictionary<Concepto, int> TotalMinutosPorConcepto =>
        DesglosePorFranja
            .SelectMany(f => f.MinutosPorConcepto)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

    // CA-4: lista vacia, RetardoTotal = DetalleRetardo.Vacio, FranjasAnomalas = 0.
    // Usado cuando no hay turno o todas las franjas son anomalas.
    public static readonly DesgloseHoras Vacio = new([], DetalleRetardo.Vacio, 0);

    // Issue #183: el desglose rico se discrimina a si mismo en el payload primitivo HorasDiscriminadas
    // (Tell-don't-Ask: el contrato plano lo produce el propio desglose, no un servicio externo).
    // CA-2: vuelca TotalMinutosPorConcepto con clave = Concepto.ToString() y valor = minutos agregados.
    // CA-3: agrega la clave literal "Retardo" con RetardoTotal.RetardoNeto solo cuando es > 0.
    //       Trazabilidad queda como lista vacia en este issue.
    public HorasDiscriminadas Discriminar() => throw new NotImplementedException();
}
