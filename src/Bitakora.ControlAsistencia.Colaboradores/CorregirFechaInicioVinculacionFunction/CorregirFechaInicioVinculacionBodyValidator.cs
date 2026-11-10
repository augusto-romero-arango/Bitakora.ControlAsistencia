using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;

public class CorregirFechaInicioVinculacionBodyValidator
    : AbstractValidator<CorregirFechaInicioVinculacionBody>
{
    public CorregirFechaInicioVinculacionBodyValidator()
    {
        // El default de DateOnly (0001-01-01) equivale a "no llego".
        RuleFor(x => x.FechaCorregida).NotEqual(default(DateOnly));
    }
}
