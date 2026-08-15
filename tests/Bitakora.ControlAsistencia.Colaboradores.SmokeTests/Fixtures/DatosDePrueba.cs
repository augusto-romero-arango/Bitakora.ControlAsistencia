namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

// Generadores de datos de ARRANGE compartidos por los smoke tests del dominio. No son oraculos:
// lo que cada test verifica (clave de stream, contenido del evento) se sigue recomponiendo a mano
// en su propia clase, para que un cambio de formato en un VO no se auto-valide (MEF-ADR-0002).
//
// Issue #387: el codigo de la vinculacion ahora es URL-safe (unreserved RFC 3986: A-Z a-z 0-9 - . _ ~,
// MEF-ADR-0043 seccion 1), lo que obligo a corregir a mano el mismo helper copiado en 11 clases
// (venia con prefijo "[TEST]-", con corchetes fuera del set permitido). Ese cambio simultaneo, que
// dejo los 11 sitios identicos, es el momento natural de la extraccion segun MEF-ADR-0018: la
// proxima invariante del codigo se tocara en un solo lugar.
internal static class DatosDePrueba
{
    // Prefijo "TEST-" (no "[TEST]-"): los corchetes no son unreserved characters y el borde los
    // rechaza con 400 desde el issue #387.
    internal static string NuevoCodigoColaborador() => $"TEST-{Guid.CreateVersion7()}";
}
