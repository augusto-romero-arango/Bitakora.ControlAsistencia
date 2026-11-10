using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

public record QuitarSubFranja(
    Guid TurnoId,
    TimeOnly Franja,
    TipoSubFranja Tipo,
    TimeOnly Inicio);
