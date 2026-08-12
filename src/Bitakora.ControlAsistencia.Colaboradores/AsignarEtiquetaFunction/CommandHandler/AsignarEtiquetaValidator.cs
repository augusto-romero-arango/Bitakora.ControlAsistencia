using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;

// Issue #355: validacion de forma del comando AsignarEtiqueta en el borde (MEF-ADR-0004 capa 1 ->
// 400 BadRequest). CA-7: TipoIdentificacion (requerido + en la lista cerrada), NumeroIdentificacion,
// Categoria y Valor requeridos -- las invariantes de doble forma/normalizacion viven en el VO
// Etiqueta (#353), no en este validator (el aggregate solo gobierna el diccionario).
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
// STUB (fase roja, issue #355): sin reglas todavia -- el implementer las agrega (precedente
// AnularTerminacionValidator, issue #354).
public class AsignarEtiquetaValidator : AbstractValidator<AsignarEtiqueta>
{
}
