using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-122: Value object que representa el resultado de depurar marcaciones contra una franja ordinaria.
// No viaja entre dominios - vive en ControlHoras junto al aggregate que lo usa.
// El DTO para cruzar fronteras (DetalleControlFranja) se crea en #108.
public record ControlFranja(DetalleFranjaOrdinaria Programada, DateTime? Entrada, DateTime? Salida)
{
    // CA-9: propiedad calculada; anomala cuando falta Entrada o Salida
    public bool EsAnomala => Entrada is null || Salida is null;
}
