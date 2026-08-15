using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction.CommandHandler;

// Issue #350: validacion de forma del comando ReingresarColaborador en el borde (MEF-ADR-0004
// capa 1 -> 400 BadRequest). CA-6: TipoIdentificacion (requerido + en la lista cerrada -- la
// normalizacion trim+MAYUSCULAS vive dentro de TipoIdentificacion.Desde, issue #371, mismo criterio
// que los demas validators del dominio), NumeroIdentificacion, CodigoColaborador y
// FechaInicio requeridos.
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
//
// Issue #387: CodigoColaborador ademas debe ser URL-safe (Cascade(Stop): NotEmpty primero, la regla
// de caracteres solo evalua un valor no vacio) -- ver ValidacionesCompartidas para el detalle del
// set permitido.
public class ReingresarColaboradorValidator : AbstractValidator<ReingresarColaborador>
{
    public ReingresarColaboradorValidator()
    {
        // Cascade(Stop) evita que Must evalue un valor vacio que NotEmpty ya rechazo -- mismo
        // criterio que TerminarVinculacionValidator.
        RuleFor(x => x.TipoIdentificacion)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(EsTipoIdentificacionReconocido)
            .WithMessage("El tipo de identificacion no es uno de los reconocidos");

        RuleFor(x => x.NumeroIdentificacion).NotEmpty();

        RuleFor(x => x.CodigoColaborador)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .DebeSerCodigoColaboradorUrlSafe();

        // FechaInicio es REQUERIDA -- el default de DateOnly (0001-01-01) equivale a "no llego"
        // (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
        RuleFor(x => x.FechaInicio).NotEqual(default(DateOnly));
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
