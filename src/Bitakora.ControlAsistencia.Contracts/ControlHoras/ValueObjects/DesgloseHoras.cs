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
        throw new NotImplementedException();

    // CA-4: lista vacia, RetardoTotal = DetalleRetardo.Vacio, FranjasAnomalas = 0.
    // Usado cuando no hay turno o todas las franjas son anomalas.
    public static DesgloseHoras Vacio =>
        throw new NotImplementedException();
}
