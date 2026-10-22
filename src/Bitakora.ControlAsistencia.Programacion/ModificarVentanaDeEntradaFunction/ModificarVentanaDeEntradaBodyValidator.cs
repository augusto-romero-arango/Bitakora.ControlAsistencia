using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction;

public class ModificarVentanaDeEntradaBodyValidator : AbstractValidator<ModificarVentanaDeEntradaBody>
{
    public ModificarVentanaDeEntradaBodyValidator()
    {
        RuleFor(x => x.Amplitud).NotNull();
    }
}
