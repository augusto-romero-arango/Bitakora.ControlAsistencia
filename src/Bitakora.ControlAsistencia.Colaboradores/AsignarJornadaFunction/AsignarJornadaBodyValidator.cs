using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction;

public class AsignarJornadaBodyValidator : AbstractValidator<AsignarJornadaBody>
{
    public AsignarJornadaBodyValidator()
    {
        RuleFor(body => body.JornadaId).NotEmpty();
    }
}
