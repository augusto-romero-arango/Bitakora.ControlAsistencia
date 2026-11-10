namespace Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

// Unica forma en que el colaborador cruza el bus interno del BC. Su nombre simple difiere del de sus
// gemelos en las otras islas (ColaboradorProgramado, ColaboradorSolicitado) para que un using
// equivocado no compile (CA-ADR-0029); Colaborador queda para el concepto rico del dominio.
// Identificacion es el stream key del maestro, formato "{Tipo}-{Numero}" (ej. "CC-79543210").
// Sin Equals custom porque todos los campos son string: si el record gana una coleccion, la igualdad
// por defecto pasa a comparar por referencia (MEF-ADR-0012).
public record ResumenColaborador(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
