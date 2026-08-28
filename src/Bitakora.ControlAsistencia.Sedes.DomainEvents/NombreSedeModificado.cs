namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Issue #457 (MEF-ADR-0043 paso 2): reemplazo completo del nombre de una sede existente.
// "Modificado" y no "Corregido": el nombre cambia legitimamente (rebranding), no solo por error
// (decision de sesion 2026-08-27, glosario termino Sede).
public record NombreSedeModificado(string Nombre);
