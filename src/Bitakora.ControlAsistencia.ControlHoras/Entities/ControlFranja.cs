using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-122: Value object que representa el resultado de depurar marcaciones contra una franja ordinaria.
// No viaja entre dominios - vive en ControlHoras junto al aggregate que lo usa.
// El DTO para cruzar fronteras (DetalleControlFranja) se crea en #108.
public record ControlFranja(DetalleFranjaOrdinaria Programada, DateTime? Entrada, DateTime? Salida)
{
    // CA-9: propiedad calculada; anomala cuando falta Entrada o Salida
    public bool EsAnomala => Entrada is null || Salida is null;

    // HU-136: capa 4 (final) del desglose de franja. Frontera unica DateTime -> MomentoDelDia.
    // Integra la lista base de ClasificadorTrabajo (#135) con retardo, excedente bruto y
    // compensacion intra-franja. Retorna null si la franja es anomala (entrada o salida nula).
    public DesgloseFranja? CalcularDesglose(DateOnly fecha, Func<DateOnly, bool> esFestivo) =>
        throw new NotImplementedException();
}
