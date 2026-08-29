namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;

// Issue #489: comando del acto de aprobar el dia completo (todas las franjas, las horas, todo el
// expediente). Decisiones trae, por franja en conflicto, el codigo de sede elegido -- el aggregate
// resuelve nombre y centro de costos desde sus propias candidatas (Tell-don't-Ask, MEF-ADR-0012).
// Vacia o ausente cuando el dia no tiene conflictos de sede pendientes (CA-1/CA-7).
public sealed record AprobarDia(
    string CodigoColaborador,
    DateOnly Fecha,
    IReadOnlyList<AprobarDia.DecisionDeSede> Decisiones)
{
    public sealed record DecisionDeSede(TimeOnly HoraInicioProgramada, string CodigoSede);
}
