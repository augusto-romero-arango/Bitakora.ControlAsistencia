using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;

// Issue #349: validacion de forma del comando TerminarVinculacion en el borde (MEF-ADR-0004
// capa 1 -> 400 BadRequest). CA-6: TipoIdentificacion (requerido + en la lista cerrada --
// normalizar trim+MAYUSCULAS antes de TipoIdentificacion.Desde, mismo criterio de normalizacion
// de entrada que RegistrarColaboradorValidator), NumeroIdentificacion y FechaEfectiva requeridos.
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura:
// no requiere tocar el wiring de DI.
// STUB (fase roja, issue #349): sin reglas todavia -- el implementer las agrega (precedente
// RegistrarColaboradorValidator, issue #330).
public class TerminarVinculacionValidator : AbstractValidator<TerminarVinculacion>
{
}
