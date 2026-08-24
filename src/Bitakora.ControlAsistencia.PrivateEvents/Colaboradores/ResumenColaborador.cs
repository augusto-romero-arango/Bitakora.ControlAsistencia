namespace Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

// Unica forma en que el colaborador cruza el bus interno del BC: la llevan DiaDepurado y
// ProgramacionTurnoDiarioSolicitada. Su nombre simple debe diferir del de sus gemelos de forma en
// las otras islas -- ColaboradorProgramado (ControlHoras.DomainEvents y Programacion.DomainEvents)
// y ColaboradorSolicitado (DTO del body de SolicitarProgramacionTurno) -- para que un using
// equivocado no compile (CA-ADR-0029: tres islas sin referencias entre si); el nombre puro
// Colaborador pertenece al concepto rico del dominio Colaboradores (anti-squatting).
//
// Identificacion es el stream key del maestro Colaboradores, con su formato "{Tipo}-{Numero}"
// (ej. "CC-79543210"). Sin Equals custom porque todos los campos son string: si el record gana una
// coleccion, la igualdad por defecto pasa a comparar por referencia (MEF-ADR-0012).
public record ResumenColaborador(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
