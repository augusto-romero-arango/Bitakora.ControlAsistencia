using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;

public class AsignarEtiquetaBodyValidator : AbstractValidator<AsignarEtiquetaBody>
{
    public AsignarEtiquetaBodyValidator()
    {
        RuleFor(x => x.Valor).NotEmpty();
    }
}
