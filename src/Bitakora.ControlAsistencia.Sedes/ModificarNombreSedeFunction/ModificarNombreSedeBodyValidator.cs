using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;

// Sin registro explicito en el contenedor: lo descubre el AddValidatorsFromAssemblyContaining de
// ComposicionServicios.
public class ModificarNombreSedeBodyValidator : AbstractValidator<ModificarNombreSedeBody>
{
    public ModificarNombreSedeBodyValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty();
    }
}
