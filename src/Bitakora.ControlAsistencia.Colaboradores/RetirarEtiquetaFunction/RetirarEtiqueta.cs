namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents
// como campo.
public record RetirarEtiqueta(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Categoria);
