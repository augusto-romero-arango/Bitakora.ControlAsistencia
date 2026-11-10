namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

// Discriminador de frontera: descanso y extra comparten forma y difieren solo en la lista que los
// contiene. Nunca cruza al evento: DescansoAgregado y ExtraAgregado son dos tipos distintos.
public enum TipoSubFranja
{
    Descanso,
    Extra
}
