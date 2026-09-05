using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Comando interno: el endpoint lo compone desde el {id} de la ruta mas el body, traduciendo
// QuitarSubFranjaBody.Tipo (string, frontera HTTP) al enum interno TipoSubFranja. Reusa el mismo
// discriminador de #603 -- no se duplica el enum (MEF-ADR-0018).
public record QuitarSubFranja(
    Guid TurnoId,
    TimeOnly Franja,
    TipoSubFranja Tipo,
    TimeOnly Inicio);
