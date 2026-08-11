using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;

// Issue #351: validacion de forma del comando CorregirNombres en el borde (MEF-ADR-0004 capa 1 ->
// 400 BadRequest). CA-5: TipoIdentificacion (requerido + en la lista cerrada -- normalizar
// trim+MAYUSCULAS antes de TipoIdentificacion.Desde, mismo criterio de normalizacion de entrada
// que TerminarVinculacionValidator/RegistrarColaboradorValidator), NumeroIdentificacion,
// PrimerNombre y PrimerApellido requeridos no vacios. SegundoNombre/SegundoApellido son opcionales
// (NombreColaborador.Crear ya los normaliza).
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
// STUB (fase roja, issue #351): sin reglas -- el implementer las agrega.
public class CorregirNombresValidator : AbstractValidator<CorregirNombres>
{
}
