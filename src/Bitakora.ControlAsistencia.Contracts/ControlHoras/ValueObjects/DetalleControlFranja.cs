using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// HU-108: DTO plano que replica ControlFranja para cruzar fronteras de dominio.
// ControlFranja vive en el dominio interno de ControlHoras; DetalleControlFranja
// es el contrato publico que viaja en DiaCalculado hacia Service Bus y sistemas externos.
// EsAnomala es campo del record (no calculado): el aggregate copia el valor de
// ControlFranja.EsAnomala al construir el DTO. Esto desacopla el contrato de la
// logica de dominio interna (Tell-don't-Ask: el aggregate construye el DTO).
// Record con constructor publico: STJ lo serializa nativamente sin ConfigurarSerializacion (ADR-0015).
public record DetalleControlFranja(
    DetalleFranjaOrdinaria Programada,
    DateTime? Entrada,
    DateTime? Salida,
    bool EsAnomala);
