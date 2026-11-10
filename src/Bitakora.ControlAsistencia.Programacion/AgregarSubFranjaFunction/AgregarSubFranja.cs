namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

public record AgregarSubFranja(
    Guid TurnoId,
    TimeOnly Franja,
    TipoSubFranja Tipo,
    TimeOnly Inicio,
    TimeOnly Fin);
