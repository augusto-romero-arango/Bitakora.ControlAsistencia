using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;

public class IniciarVinculacionBodyValidator : AbstractValidator<IniciarVinculacionBody>
{
    public IniciarVinculacionBodyValidator()
    {
        RuleFor(x => x.CodigoColaborador)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .DebeSerCodigoColaboradorUrlSafe();

        // El default de DateOnly (0001-01-01) equivale a "no llego".
        RuleFor(x => x.FechaInicio).NotEqual(default(DateOnly));

        RuleFor(x => x.CodigoSede).NotEmpty().When(x => x.CodigoSede is not null);
    }
}
