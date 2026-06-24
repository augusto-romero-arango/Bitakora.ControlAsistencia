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

    // Clave literal del payload para el retardo neto del dia. NO es un Concepto (CA-3: el enum
    // permanece sin valor Retardo): es un identificador estable del contrato que nomina lee junto
    // a los conceptos. Por ser clave de contrato cross-domain - igual que los nombres de Concepto
    // que produce Concepto.ToString() - no se localiza ni vive en .resx: cambiarla romperia la
    // deserializacion del consumidor.
    private const string ClaveRetardo = "Retardo";

    // Issue #183: el desglose rico se discrimina a si mismo en el payload primitivo HorasDiscriminadas
    // (Tell-don't-Ask: el contrato plano lo produce el propio desglose, no un servicio externo).
    // CA-2: vuelca TotalMinutosPorConcepto con clave = Concepto.ToString() y valor = minutos agregados.
    // CA-3: agrega la clave literal "Retardo" con RetardoTotal.RetardoNeto solo cuando es > 0.
    //       Trazabilidad queda como lista vacia en este issue.
    public HorasDiscriminadas Discriminar()
    {
        var minutosPorConcepto = TotalMinutosPorConcepto
            .ToDictionary(concepto => concepto.Key.ToString(), concepto => concepto.Value);

        if (RetardoTotal.RetardoNeto > 0)
            minutosPorConcepto[ClaveRetardo] = RetardoTotal.RetardoNeto;

        return new HorasDiscriminadas(minutosPorConcepto, []);
    }
}
