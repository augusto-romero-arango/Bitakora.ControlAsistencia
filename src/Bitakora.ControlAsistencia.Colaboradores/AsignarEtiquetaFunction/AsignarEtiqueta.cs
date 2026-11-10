namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;

// Payload primitivo (MEF-ADR-0039 decision 6): nunca reusa un tipo de Colaboradores.DomainEvents
// como campo.
public record AsignarEtiqueta(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Categoria,
    string Valor);
