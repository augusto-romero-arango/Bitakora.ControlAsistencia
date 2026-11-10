using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

// Constructor primario publico: STJ lo serializa sin ConfigurarSerializacion.
public record DesgloseFranja(
    FranjaProgramada Programada,
    IReadOnlyList<IntervaloClasificado> Intervalos,
    Retardo Retardo)
{
    // Los conceptos que no aparecen en Intervalos no figuran en el diccionario.
    public IReadOnlyDictionary<Concepto, int> MinutosPorConcepto =>
        Intervalos
            .GroupBy(i => i.Concepto)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.DuracionEnMinutos));
}
