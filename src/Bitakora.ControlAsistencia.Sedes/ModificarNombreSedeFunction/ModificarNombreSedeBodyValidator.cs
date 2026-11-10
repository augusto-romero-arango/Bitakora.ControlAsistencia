using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;

public class ModificarNombreSedeBodyValidator : AbstractValidator<ModificarNombreSedeBody>
{
    public ModificarNombreSedeBodyValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty();
    }
}
