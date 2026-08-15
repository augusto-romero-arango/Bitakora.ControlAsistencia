using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;

// Issue #379 (MEF-ADR-0043 paso 4, CA-2): validacion de forma del body reducido en el borde
// (MEF-ADR-0004 capa 1 -> 400 BadRequest). Reemplaza a TerminarVinculacionValidator (eliminado,
// vivia en CommandHandler/): aquel validaba TipoIdentificacion/NumeroIdentificacion/FechaEfectiva
// cuando la identificacion llegaba en el body; ahora TipoIdentificacion/NumeroIdentificacion se
// derivan de {id} (parseo tipado en el propio FunctionEndpoint, precedente
// CorregirNombresFunction.FunctionEndpoint post-#377) y Codigo se deriva de {codigo} -- lo que
// queda en el body es unicamente FechaEfectiva.
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
public class TerminarVinculacionBodyValidator : AbstractValidator<TerminarVinculacionBody>
{
    public TerminarVinculacionBodyValidator()
    {
        // FechaEfectiva es REQUERIDA -- el default de DateOnly (0001-01-01) equivale a "no llego"
        // (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
        RuleFor(x => x.FechaEfectiva).NotEqual(default(DateOnly));
    }
}
