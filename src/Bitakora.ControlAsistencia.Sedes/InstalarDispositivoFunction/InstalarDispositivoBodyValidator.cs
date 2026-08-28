using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;

public class InstalarDispositivoBodyValidator : AbstractValidator<InstalarDispositivoBody>
{
    public InstalarDispositivoBodyValidator()
    {
        RuleFor(x => x.DispositivoId).NotEmpty();
    }
}
