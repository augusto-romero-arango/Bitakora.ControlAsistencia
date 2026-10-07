using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction;

public class ModificarLimitesJornadaBodyValidator : AbstractValidator<ModificarLimitesJornadaBody>
{
    public ModificarLimitesJornadaBodyValidator()
    {
        RuleFor(x => x.HorasSemanales).NotNull();
        RuleFor(x => x.TopeDiario).NotNull();
        RuleFor(x => x.MinimoDiario).NotNull();
    }
}
