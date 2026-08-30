namespace Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction;

// Copia propia del feature folder, no reuso del ColaboradorSolicitado de
// SolicitarProgramacionTurnoFunction: cada comando posee el DTO de su request, asi un cambio de
// forma en el body de uno no arrastra al otro.
public record ColaboradorSolicitado(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
