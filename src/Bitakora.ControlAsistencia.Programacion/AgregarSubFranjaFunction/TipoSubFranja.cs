namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

// Discriminador de frontera (MEF-ADR-0043): descanso y extra comparten forma exacta y difieren
// solo en la lista que los contiene. Nunca cruza al evento -- DescansoAgregado y ExtraAgregado son
// dos tipos distintos.
// public, no internal: AgregarSubFranja (comando public, mismo patron que AgregarFranja/CrearTurno)
// lo expone como parametro -- un enum internal en un record public no compila (CS0051).
public enum TipoSubFranja
{
    Descanso,
    Extra
}
