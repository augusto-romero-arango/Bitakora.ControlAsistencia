using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;

// Issue #378 (MEF-ADR-0043 paso 1, CA-3): validacion de forma del body reducido en el borde
// (MEF-ADR-0004 capa 1 -> 400 BadRequest). Reemplaza a ReingresarColaboradorValidator (eliminado,
// vivia en CommandHandler/ del comando absorbido, issue #350): TipoIdentificacion/
// NumeroIdentificacion ya no llegan en el body -- se validan en el FunctionEndpoint via
// Identificacion.Parsear -- lo que queda en el body son CodigoColaborador y FechaInicio.
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
//
// CodigoColaborador ademas debe ser URL-safe (Cascade(Stop): NotEmpty primero, la regla de
// caracteres solo evalua un valor no vacio) -- ver ValidacionesCompartidas para el detalle del set
// permitido (issue #387).
public class IniciarVinculacionBodyValidator : AbstractValidator<IniciarVinculacionBody>
{
    public IniciarVinculacionBodyValidator()
    {
        RuleFor(x => x.CodigoColaborador)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .DebeSerCodigoColaboradorUrlSafe();

        // FechaInicio es REQUERIDA -- el default de DateOnly (0001-01-01) equivale a "no llego"
        // (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
        RuleFor(x => x.FechaInicio).NotEqual(default(DateOnly));

        // CA-6: CodigoSede es opcional -- ausente (null) es valido; presente exige un valor no
        // vacio/blanco (NotEmpty tambien rechaza whitespace-only en FluentValidation).
        RuleFor(x => x.CodigoSede).NotEmpty().When(x => x.CodigoSede is not null);
    }
}
