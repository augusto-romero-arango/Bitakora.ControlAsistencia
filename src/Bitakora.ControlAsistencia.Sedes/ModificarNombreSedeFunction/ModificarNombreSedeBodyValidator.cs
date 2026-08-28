using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;

// Issue #457 (CA-2): Nombre vacio -> 400 en el borde (MEF-ADR-0004 capa 1). Se descubre via el
// AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura.
public class ModificarNombreSedeBodyValidator : AbstractValidator<ModificarNombreSedeBody>
{
    public ModificarNombreSedeBodyValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty();
    }
}
