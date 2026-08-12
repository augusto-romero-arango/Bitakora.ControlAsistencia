using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;

// Issue #355: validacion de forma del comando RetirarEtiqueta en el borde (MEF-ADR-0004 capa 1 ->
// 400 BadRequest). CA-7: TipoIdentificacion (requerido + en la lista cerrada), NumeroIdentificacion
// y Categoria requeridos. Sin Valor: el comando no lo lleva (retirar solo necesita la categoria).
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
// STUB (fase roja, issue #355): sin reglas todavia -- el implementer las agrega (precedente
// AnularTerminacionValidator, issue #354).
public class RetirarEtiquetaValidator : AbstractValidator<RetirarEtiqueta>
{
}
