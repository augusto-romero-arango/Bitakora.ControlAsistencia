using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction;

public class AsignarJornadaPredeterminadaBodyValidator : AbstractValidator<AsignarJornadaPredeterminadaBody>
{
    public AsignarJornadaPredeterminadaBodyValidator()
    {
        RuleFor(x => x.JornadaId).NotEmpty();
    }
}
