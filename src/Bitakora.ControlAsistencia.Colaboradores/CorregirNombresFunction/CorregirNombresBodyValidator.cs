using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;

public class CorregirNombresBodyValidator : AbstractValidator<CorregirNombresBody>
{
    public CorregirNombresBodyValidator()
    {
        RuleFor(x => x.PrimerNombre).NotEmpty();
        RuleFor(x => x.PrimerApellido).NotEmpty();
    }
}
