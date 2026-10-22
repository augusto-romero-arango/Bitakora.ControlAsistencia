namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// El event store persiste el enum como entero (EnumStorage.AsInteger): los valores son contrato de
// los eventos ya escritos y no se reordenan.
public enum DiaVecino
{
    Vispera = 0,
    DiaSiguiente = 1
}
