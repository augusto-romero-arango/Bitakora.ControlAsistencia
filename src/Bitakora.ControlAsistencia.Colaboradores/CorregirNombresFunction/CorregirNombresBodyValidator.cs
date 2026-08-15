using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;

// Issue #377 (MEF-ADR-0043 paso 2, CA-4): validacion de forma del body reducido en el borde
// (MEF-ADR-0004 capa 1 -> 400 BadRequest). Reemplaza a CorregirNombresValidator (eliminado, vivia en
// CommandHandler/): aquel validaba el comando completo (6 campos) cuando TipoIdentificacion/
// NumeroIdentificacion llegaban en el body; ahora esos 2 campos vienen de la ruta y se validan con
// el parseo tipado de Identificacion.Parsear (400 explicito en el propio FunctionEndpoint,
// precedente AsignarEtiquetaFunction.FunctionEndpoint post-#376) -- lo que queda en el body son los
// 4 campos del nombre. SegundoNombre/SegundoApellido son opcionales (NombreColaborador.Crear ya los
// normaliza); solo PrimerNombre/PrimerApellido son obligatorios (minimo colombiano, #348).
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
public class CorregirNombresBodyValidator : AbstractValidator<CorregirNombresBody>
{
    public CorregirNombresBodyValidator()
    {
        RuleFor(x => x.PrimerNombre).NotEmpty();
        RuleFor(x => x.PrimerApellido).NotEmpty();
    }
}
