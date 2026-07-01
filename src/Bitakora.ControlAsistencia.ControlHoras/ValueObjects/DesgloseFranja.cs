using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Issue #129: Estructura agregada del desglose de una sola franja.
// Record con constructor primario publico - STJ lo serializa nativamente sin ConfigurarSerializacion (ADR-0015).
// Las propiedades calculadas se recalculan en cada acceso desde los parametros del ctor.
public record DesgloseFranja(
    DetalleFranjaOrdinaria Programada,
    IReadOnlyList<IntervaloClasificado> Intervalos,
    Retardo Retardo)
{
    // CA-1, CA-2: agrupa Intervalos por Concepto y suma DuracionEnMinutos.
    // Los conceptos que no aparecen en Intervalos no figuran en el diccionario.
    public IReadOnlyDictionary<Concepto, int> MinutosPorConcepto =>
        Intervalos
            .GroupBy(i => i.Concepto)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.DuracionEnMinutos));
}
