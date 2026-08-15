using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;

// Issue #379 (MEF-ADR-0043 paso 4, CA-4): validacion de forma del body reducido en el borde
// (MEF-ADR-0004 capa 1 -> 400 BadRequest). Reemplaza a CorregirFechaInicioVinculacionValidator
// (eliminado, vivia en CommandHandler/): aquel validaba TipoIdentificacion/NumeroIdentificacion/
// FechaCorregida cuando la identificacion llegaba en el body; ahora TipoIdentificacion/
// NumeroIdentificacion se derivan de {id} (parseo tipado en el propio FunctionEndpoint) y Codigo
// se deriva de {codigo} -- lo que queda en el body es unicamente FechaCorregida.
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
public class CorregirFechaInicioVinculacionBodyValidator
    : AbstractValidator<CorregirFechaInicioVinculacionBody>
{
    public CorregirFechaInicioVinculacionBodyValidator()
    {
        // FechaCorregida es REQUERIDA -- el default de DateOnly (0001-01-01) equivale a "no llego"
        // (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
        RuleFor(x => x.FechaCorregida).NotEqual(default(DateOnly));
    }
}
