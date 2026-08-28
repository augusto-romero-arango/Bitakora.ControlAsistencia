using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler;

// Issue #456: validacion de forma del comando RegistrarSede en el borde (MEF-ADR-0004 capa 1 ->
// 400 BadRequest). Requeridos: Codigo (ademas URL-safe, CA-4) y Nombre. Ciudad/Direccion son
// opcionales -- sin regla, se descubre solo via AddValidatorsFromAssemblyContaining.
public class RegistrarSedeValidator : AbstractValidator<RegistrarSede>
{
    public RegistrarSedeValidator()
    {
        // Cascade(Stop): NotEmpty primero, la regla de caracteres solo evalua un valor no vacio.
        RuleFor(x => x.Codigo)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .DebeSerCodigoSedeUrlSafe();

        RuleFor(x => x.Nombre).NotEmpty();
    }
}
