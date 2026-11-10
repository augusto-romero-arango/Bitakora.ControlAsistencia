using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler;

public class RegistrarSedeValidator : AbstractValidator<RegistrarSede>
{
    public RegistrarSedeValidator()
    {
        RuleFor(x => x.Codigo)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .DebeSerCodigoSedeUrlSafe();

        RuleFor(x => x.Nombre).NotEmpty();
    }
}
