// El modelo rico vive en el dominio; HorasDiscriminadas (payload plano del bus) se referencia
// explicitamente porque Discriminar() lo produce.

using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

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
    // Concepto no lo incluye): el retardo es un castigo, no tiempo trabajado, pero viaja en el mismo
    // diccionario plano.
    private const string ClaveRetardo = "Retardo";

    // Tell-don't-Ask: el desglose se discrimina a si mismo, sin exponer sus internos para que un
    // colaborador externo arme el diccionario. El diccionario solo lleva claves con valor (minutos > 0):
    // publicar un concepto en cero le daria al consumidor una clave sin significado.
    // Del modelo rico solo viajan los strings ya traducidos de Trazabilidad (ToString() de
    // IntervaloTemporal y Retardo): el modelo de dominio rico no cruza el bus (MEF-ADR-0012).
    public HorasDiscriminadas Discriminar()
    {
        var horasPorConcepto = TotalMinutosPorConcepto
            .Where(par => par.Value > 0)
            .ToDictionary(par => par.Key.ToString(), par => HorasLiquidables.DesdeMinutos(par.Value));

        var trazabilidad = ConstruirTrazabilidadPorConcepto();

        if (RetardoTotal.RetardoNeto > 0)
        {
            horasPorConcepto[ClaveRetardo] = HorasLiquidables.DesdeMinutos(RetardoTotal.RetardoNeto);
            trazabilidad.Add(RetardoTotal.ToString());
        }

        return new HorasDiscriminadas(horasPorConcepto, trazabilidad);
    }

    // Una linea por concepto presente en el dia, con sus intervalos en orden cronologico y la etiqueta
    // humana traducida una sola vez. Como IntervaloTemporal garantiza Inicio < Fin, cada concepto
    // presente aporta minutos > 0: por eso hay tantas lineas como claves de HorasPorConcepto (sin
    // contar "Retardo").
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
