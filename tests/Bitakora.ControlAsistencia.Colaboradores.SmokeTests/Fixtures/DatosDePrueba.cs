namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

// Datos de ARRANGE, no oraculos: lo que cada test verifica (clave de stream, contenido del evento) se
// recompone a mano en su clase, para que un cambio de formato en un VO no se auto-valide (MEF-ADR-0002).
internal static class DatosDePrueba
{
    // El codigo debe ser URL-safe (unreserved RFC 3986: A-Z a-z 0-9 - . _ ~, MEF-ADR-0043 seccion 1):
    // con "[TEST]-" el borde responde 400.
    internal static string NuevoCodigoColaborador() => $"TEST-{Guid.CreateVersion7()}";
}
