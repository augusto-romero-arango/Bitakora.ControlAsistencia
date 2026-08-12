using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;

// Issue #354: validacion de forma del comando AnularTerminacion en el borde (MEF-ADR-0004 capa 1
// -> 400 BadRequest). CA-6: TipoIdentificacion (requerido + en la lista cerrada) y
// NumeroIdentificacion requeridos. Sin FechaEfectiva ni otro campo: el comando no lleva payload
// propio.
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura:
// no requiere tocar el wiring de DI.
// STUB (fase roja, issue #354): sin reglas todavia -- el implementer las agrega (precedente
// TerminarVinculacionValidator, issue #349; RegistrarColaboradorValidator, issue #330).
public class AnularTerminacionValidator : AbstractValidator<AnularTerminacion>
{
}
