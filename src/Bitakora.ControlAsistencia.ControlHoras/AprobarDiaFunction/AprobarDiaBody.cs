namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;

// Issue #489 (MEF-ADR-0043 paso 4): body reducido a Decisiones -- CodigoColaborador/Fecha se
// derivan de la ruta (control-horas/depuraciones/{codigoColaborador}/{fecha}:aprobar). Nullable:
// puede venir vacio o ausente cuando el dia no tiene conflictos de sede (CA-1/CA-7).
public sealed record AprobarDiaBody(IReadOnlyList<AprobarDia.DecisionDeSede>? Decisiones);
