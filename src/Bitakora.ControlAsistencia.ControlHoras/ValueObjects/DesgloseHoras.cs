using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Constructor primario publico: STJ lo serializa sin ConfigurarSerializacion.
// FranjasAnomalas: franjas excluidas por falta de entrada o salida.
// RetardoTotal: consolidado del dia, con compensacion cruzada entre franjas.
public record DesgloseHoras(
    IReadOnlyList<DesgloseFranja> DesglosePorFranja,
    Retardo RetardoTotal,
    int FranjasAnomalas)
{
    public IReadOnlyDictionary<Concepto, int> TotalMinutosPorConcepto =>
        DesglosePorFranja
            .SelectMany(f => f.MinutosPorConcepto)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

    public static readonly DesgloseHoras Vacio = new([], Retardo.Vacio, 0);

    // Clave literal del retardo en MinutosPorConcepto. No es un Concepto del calculo de horas (el enum
    // Concepto no lo incluye): el retardo es un castigo, no tiempo trabajado, pero viaja en el mismo
    // diccionario plano.
    private const string ClaveRetardo = "Retardo";

    // El diccionario solo lleva claves con valor (minutos > 0): un concepto en cero le daria al
    // consumidor una clave sin significado. Del modelo rico solo viajan los strings ya traducidos de
    // Trazabilidad: el modelo de dominio rico no cruza el bus (CA-ADR-0025).
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
