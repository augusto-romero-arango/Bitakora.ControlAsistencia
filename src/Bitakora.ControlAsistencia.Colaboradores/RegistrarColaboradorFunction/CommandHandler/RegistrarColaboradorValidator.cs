using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;

// Issue #330: validacion de forma del comando RegistrarColaborador en el borde (MEF-ADR-0004 capa 1
// -> 400 BadRequest). Requeridos: TipoIdentificacion (no vacio y en la lista cerrada),
// NumeroIdentificacion, PrimerNombre, PrimerApellido, CodigoColaborador y FechaInicio.
// SegundoNombre/SegundoApellido son opcionales (NombreColaborador.Crear ya los normaliza).
// Se descubre solo via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura:
// no requiere tocar el wiring de DI.
public class RegistrarColaboradorValidator : AbstractValidator<RegistrarColaborador>
{
    public RegistrarColaboradorValidator()
    {
        // CA-3/CA-4: requerido y en la lista cerrada -- pero "cc" (minusculas) debe seguir siendo
        // valido, la normalizacion trim+MAYUSCULAS ocurre ANTES de consultar la lista cerrada
        // (mismo criterio de normalizacion de entrada que el handler, MEF-ADR-0037 seccion 2).
        // Cascade(Stop) evita que Must evalue un valor vacio que NotEmpty ya rechazo.
        RuleFor(x => x.TipoIdentificacion)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(EsTipoIdentificacionReconocido)
            .WithMessage("El tipo de identificacion no es uno de los reconocidos");

        RuleFor(x => x.NumeroIdentificacion).NotEmpty();
        RuleFor(x => x.PrimerNombre).NotEmpty();
        RuleFor(x => x.PrimerApellido).NotEmpty();
        RuleFor(x => x.CodigoColaborador).NotEmpty();

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
            TipoIdentificacion.Desde(tipo.Trim().ToUpperInvariant());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
