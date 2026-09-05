namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

// Comando interno: el endpoint lo compone desde el {id} de la ruta mas el body, traduciendo
// AgregarSubFranjaBody.Tipo (string, frontera HTTP) al enum interno TipoSubFranja.
public record AgregarSubFranja(
    Guid TurnoId,
    TimeOnly Franja,
    TipoSubFranja Tipo,
    TimeOnly Inicio,
    TimeOnly Fin);
