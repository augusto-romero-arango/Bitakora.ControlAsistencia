// Issue #185: el modelo rico vive en el dominio; HorasDiscriminadas (payload plano) se queda en
// Contracts y se referencia explicitamente porque Discriminar() lo produce.

using Bitakora.ControlAsistencia.PublicEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Issue #129: Estructura agregada del desglose del dia completo.
// Record con constructor primario publico - STJ lo serializa nativamente sin ConfigurarSerializacion (ADR-0015).
// FranjasAnomalas: franjas excluidas por falta de entrada o salida.
// RetardoTotal: consolidado del dia; lo calcula #116 con compensacion cruzada.
public record DesgloseHoras(
    IReadOnlyList<DesgloseFranja> DesglosePorFranja,
    Retardo RetardoTotal,
    int FranjasAnomalas)
{
    // CA-3: suma elemento a elemento de MinutosPorConcepto de cada DesgloseFranja.
    public IReadOnlyDictionary<Concepto, int> TotalMinutosPorConcepto =>
        DesglosePorFranja
            .SelectMany(f => f.MinutosPorConcepto)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

    // CA-4: lista vacia, RetardoTotal = Retardo.Vacio, FranjasAnomalas = 0.
    // Usado cuando no hay turno o todas las franjas son anomalas.
    public static readonly DesgloseHoras Vacio = new([], Retardo.Vacio, 0);

    // Clave literal del retardo en MinutosPorConcepto. No es un Concepto del calculo de horas (el enum
    // Concepto no lo incluye, CA-3): el retardo es un castigo, no tiempo trabajado, pero viaja en el
    // mismo diccionario plano hacia nomina.
    private const string ClaveRetardo = "Retardo";

    // Issue #183: traduce el desglose rico al payload plano que viaja en DiaCalculado hacia nomina.
    // Tell-don't-Ask: el desglose se discrimina a si mismo (no se exponen sus internos para que un
    // colaborador externo arme el diccionario). Vuelca TotalMinutosPorConcepto con clave Concepto.ToString()
    // y agrega la clave literal "Retardo" con RetardoTotal.RetardoNeto solo cuando es > 0.
    //
    // Issue #184: ademas puebla Trazabilidad, la memoria de calculo legible para nomina. Una linea por
    // item con minutos > 0: por concepto, derivada de los ToString() ricos (IntervaloTemporal + etiqueta
    // .resx); por retardo, RetardoTotal.ToString() cuando el neto es > 0. Solo viajan los strings ya
    // traducidos: el modelo de dominio rico no cruza el bus, pero su ToString() si.
    public HorasDiscriminadas Discriminar()
    {
        var minutosPorConcepto = TotalMinutosPorConcepto
            .Where(par => par.Value > 0)
            .ToDictionary(par => par.Key.ToString(), par => par.Value);

        var trazabilidad = ConstruirTrazabilidadPorConcepto();

        if (RetardoTotal.RetardoNeto > 0)
        {
            minutosPorConcepto[ClaveRetardo] = RetardoTotal.RetardoNeto;
            trazabilidad.Add(RetardoTotal.ToString());
        }

        return new HorasDiscriminadas(minutosPorConcepto, trazabilidad);
    }

    // Una linea por concepto presente en el dia: sus intervalos (de todas las franjas, en orden
    // cronologico) renderizados via ToString() y la etiqueta humana traducida una sola vez. Como cada
    // intervalo dura > 0min, hay exactamente un concepto por clave de MinutosPorConcepto (sin contar
    // "Retardo"). Para un concepto con un unico intervalo coincide con IntervaloClasificado.ToString().
    private List<string> ConstruirTrazabilidadPorConcepto() =>
        DesglosePorFranja
            .SelectMany(franja => franja.Intervalos)
            .GroupBy(intervalo => intervalo.Concepto)
            .Select(LineaConcepto)
            .ToList();

    private static string LineaConcepto(IGrouping<Concepto, IntervaloClasificado> grupo)
    {
        var intervalos = string.Join(", ", grupo.OrderBy(ic => ic.Intervalo).Select(ic => ic.Intervalo));
        return $"{intervalos}: {IntervaloClasificado.Mensajes.Etiqueta(grupo.Key)}";
    }
}
