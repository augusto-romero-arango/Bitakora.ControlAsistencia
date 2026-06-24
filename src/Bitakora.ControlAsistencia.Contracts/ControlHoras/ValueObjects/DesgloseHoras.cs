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

    // Clave literal del retardo en MinutosPorConcepto. No es un Concepto del calculo de horas (el enum
    // Concepto no lo incluye, CA-3): el retardo es un castigo, no tiempo trabajado, pero viaja en el
    // mismo diccionario plano hacia nomina.
    private const string ClaveRetardo = "Retardo";

    // Issue #183: traduce el desglose rico al payload plano que viaja en DiaCalculado hacia nomina.
    // Tell-don't-Ask: el desglose se discrimina a si mismo (no se exponen sus internos para que un
    // colaborador externo arme el diccionario). Vuelca TotalMinutosPorConcepto con clave Concepto.ToString()
    // y agrega la clave literal "Retardo" con RetardoTotal.RetardoNeto solo cuando es > 0. Trazabilidad
    // viaja vacia en este issue.
    public HorasDiscriminadas Discriminar()
    {
        var minutosPorConcepto = TotalMinutosPorConcepto
            .Where(par => par.Value > 0)
            .ToDictionary(par => par.Key.ToString(), par => par.Value);

        if (RetardoTotal.RetardoNeto > 0)
            minutosPorConcepto[ClaveRetardo] = RetardoTotal.RetardoNeto;

        return new HorasDiscriminadas(minutosPorConcepto, []);
    }
}
