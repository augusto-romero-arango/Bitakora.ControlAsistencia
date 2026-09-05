using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Comando interno: el endpoint lo compone desde el {id} de la ruta mas el body, traduciendo
// QuitarSubFranjaBody.Tipo (string, frontera HTTP) al enum interno TipoSubFranja.
public record QuitarSubFranja(
    Guid TurnoId,
    TimeOnly Franja,
    TipoSubFranja Tipo,
    TimeOnly Inicio);
