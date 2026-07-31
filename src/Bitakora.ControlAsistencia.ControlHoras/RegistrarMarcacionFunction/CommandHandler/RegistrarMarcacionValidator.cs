using FluentValidation;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;

// Issue #279: validacion de forma del comando RegistrarMarcacion en el borde.
// CA-1: descubierto por AddValidatorsFromAssemblyContaining ya registrado en
// ComposicionServicios (no requiere tocar el wiring de DI).
// STUB (fase roja): sin reglas todavia - las agrega la fase verde (CA-2, CA-3, CA-4).
public class RegistrarMarcacionValidator : AbstractValidator<RegistrarMarcacion>
{
    public RegistrarMarcacionValidator()
    {
    }
}
