using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction.CommandHandler;

// Issue #354: validacion de forma del comando AnularTerminacion en el borde (MEF-ADR-0004 capa 1
// -> 400 BadRequest). CA-6: TipoIdentificacion (requerido + en la lista cerrada -- normalizar
// trim+MAYUSCULAS antes de TipoIdentificacion.Desde, mismo criterio de normalizacion de entrada
// que TerminarVinculacionValidator) y NumeroIdentificacion requeridos. Sin FechaEfectiva ni otro
// campo: el comando no lleva payload propio.
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura:
// no requiere tocar el wiring de DI.
public class AnularTerminacionValidator : AbstractValidator<AnularTerminacion>
{
    public AnularTerminacionValidator()
    {
        // Cascade(Stop) evita que Must evalue un valor vacio que NotEmpty ya rechazo -- mismo
        // criterio que TerminarVinculacionValidator.
        RuleFor(x => x.TipoIdentificacion)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(EsTipoIdentificacionReconocido)
            .WithMessage("El tipo de identificacion no es uno de los reconocidos");

        RuleFor(x => x.NumeroIdentificacion).NotEmpty();
    }

    // Consulta la lista cerrada (#348) sin propagar la excepcion de dominio al boundary de
    // validacion -- un codigo fuera de la lista debe traducirse en un error de FluentValidation
    // (400), no en una excepcion no controlada.
    private static bool EsTipoIdentificacionReconocido(string tipo)
    {
        try
        {
            TipoIdentificacion.Desde(tipo);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
