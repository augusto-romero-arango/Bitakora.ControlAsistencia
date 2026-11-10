using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;

// CodigoColaborador y Fecha vienen de la ruta. Nullable porque un dia sin conflictos de sede se
// aprueba sin decisiones.
public sealed record AprobarDiaBody(IReadOnlyList<DecisionDeSede>? Decisiones);
