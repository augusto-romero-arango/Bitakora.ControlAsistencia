using FluentValidation;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;

// Issue #279: validacion de forma del comando RegistrarMarcacion en el borde.
// CA-1: descubierto por AddValidatorsFromAssemblyContaining ya registrado en
// ComposicionServicios (no requiere tocar el wiring de DI).
public class RegistrarMarcacionValidator : AbstractValidator<RegistrarMarcacion>
{
    public RegistrarMarcacionValidator()
    {
        // CA-2: EmpleadoId nulo, vacio o solo espacios en blanco produce 400.
        // CA-3: EmpleadoId con ':' produce 400 - ComputarStreamId usa ':' como separador
        // entre EmpleadoId y Timestamp; sin esta regla, un EmpleadoId con ':' puede
        // fabricar el mismo stream ID que otra combinacion legitima.
        RuleFor(x => x.EmpleadoId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(empleadoId => !empleadoId.Contains(':'))
            .WithMessage("EmpleadoId no puede contener ':'");

        // CA-4: Timestamp con el valor default de DateTime produce 400.
        RuleFor(x => x.Timestamp).NotEqual(default(DateTime));
    }
}
