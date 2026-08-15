using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;

// Issue #376 (MEF-ADR-0043 paso 2, CA-1): validacion de forma del body reducido en el borde
// (MEF-ADR-0004 capa 1 -> 400 BadRequest). Reemplaza a AsignarEtiquetaValidator (eliminado): aquel
// validaba el comando completo (4 campos) cuando TipoIdentificacion/NumeroIdentificacion/Categoria
// llegaban en el body; ahora esos 3 campos vienen de la ruta y se validan con el parseo tipado de
// Identificacion.Parsear (400 explicito en el propio FunctionEndpoint, precedente
// ObtenerFichaColaborador) -- lo unico que queda en el body es Valor.
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
public class AsignarEtiquetaBodyValidator : AbstractValidator<AsignarEtiquetaBody>
{
    public AsignarEtiquetaBodyValidator()
    {
        RuleFor(x => x.Valor).NotEmpty();
    }
}
