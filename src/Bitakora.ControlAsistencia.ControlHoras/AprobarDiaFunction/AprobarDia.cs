using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;

// Comando del acto de aprobar el dia completo (todas las franjas, las horas, todo el expediente).
// Decisiones va vacia o ausente cuando el dia no tiene conflictos de sede pendientes (CA-1/CA-7).
public sealed record AprobarDia(
    string CodigoColaborador,
    DateOnly Fecha,
    IReadOnlyList<DecisionDeSede> Decisiones);
