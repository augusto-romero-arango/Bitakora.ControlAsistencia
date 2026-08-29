using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;

// MEF-ADR-0043 paso 4: el body se reduce a Decisiones -- CodigoColaborador y Fecha se derivan de la
// ruta. Nullable porque un dia sin conflictos de sede se aprueba sin decisiones (CA-1/CA-7).
public sealed record AprobarDiaBody(IReadOnlyList<DecisionDeSede>? Decisiones);
