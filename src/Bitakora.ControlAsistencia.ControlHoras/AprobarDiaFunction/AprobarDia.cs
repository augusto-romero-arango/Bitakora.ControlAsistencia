using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction;

// Aprueba el dia completo (todas las franjas, las horas, todo el expediente). Decisiones va vacia
// cuando el dia no tiene conflictos de sede pendientes.
public sealed record AprobarDia(
    string CodigoColaborador,
    DateOnly Fecha,
    IReadOnlyList<DecisionDeSede> Decisiones);
