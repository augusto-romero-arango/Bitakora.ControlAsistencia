namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction;

// Comando interno: el endpoint lo compone desde el {id} de la ruta mas el body
// (MEF-ADR-0043 paso 4 -- la franja no es un sub-recurso direccionable por URL: su clave natural
// HH:mm contiene ":", fuera del charset URL-safe, y el comando lleva payload en el body).
public record QuitarFranja(Guid TurnoId, TimeOnly Franja);
