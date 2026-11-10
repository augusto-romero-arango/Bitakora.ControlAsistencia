using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;

public class TerminarVinculacionBodyValidator : AbstractValidator<TerminarVinculacionBody>
{
    public TerminarVinculacionBodyValidator()
    {
        // El default de DateOnly (0001-01-01) equivale a "no llego".
        RuleFor(x => x.FechaEfectiva).NotEqual(default(DateOnly));
    }
}
