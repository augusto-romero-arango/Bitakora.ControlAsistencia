using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction.CommandHandler;

// Issue #350: validacion de forma del comando ReingresarColaborador en el borde (MEF-ADR-0004
// capa 1 -> 400 BadRequest). CA-6: TipoIdentificacion (requerido + en la lista cerrada --
// normalizar trim+MAYUSCULAS antes de TipoIdentificacion.Desde, mismo criterio de normalizacion de
// entrada que los demas validators del dominio), NumeroIdentificacion, CodigoColaborador y
// FechaInicio requeridos.
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
// STUB (fase roja, issue #350): sin reglas todavia -- el implementer las agrega (precedente
// TerminarVinculacionValidator, issue #349).
public class ReingresarColaboradorValidator : AbstractValidator<ReingresarColaborador>
{
}
