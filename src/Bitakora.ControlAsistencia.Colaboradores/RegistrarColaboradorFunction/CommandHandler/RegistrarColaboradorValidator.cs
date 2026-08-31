using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;

// Issue #330: validacion de forma del comando RegistrarColaborador en el borde (MEF-ADR-0004 capa 1
// -> 400 BadRequest). Requeridos: TipoIdentificacion (no vacio y en la lista cerrada),
// NumeroIdentificacion, PrimerNombre, PrimerApellido, CodigoColaborador y FechaInicio.
// SegundoNombre/SegundoApellido son opcionales (NombreColaborador.Crear ya los normaliza).
// Se descubre solo via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura:
// no requiere tocar el wiring de DI.
//
// Issue #387: CodigoColaborador ademas debe ser URL-safe (Cascade(Stop): NotEmpty primero, la regla
// de caracteres solo evalua un valor no vacio) -- ver ValidacionesCompartidas para el detalle del
// set permitido.
public class RegistrarColaboradorValidator : AbstractValidator<RegistrarColaborador>
{
    public RegistrarColaboradorValidator()
    {
        // CA-3/CA-4: requerido y en la lista cerrada -- pero "cc" (minusculas) debe seguir siendo
        // valido: la normalizacion trim+MAYUSCULAS vive dentro de TipoIdentificacion.Desde
        // (issue #371), no en este borde.
        // Cascade(Stop) evita que Must evalue un valor vacio que NotEmpty ya rechazo.
        RuleFor(x => x.TipoIdentificacion)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(EsTipoIdentificacionReconocido)
            .WithMessage("El tipo de identificacion no es uno de los reconocidos");

        RuleFor(x => x.NumeroIdentificacion).NotEmpty();
        RuleFor(x => x.PrimerNombre).NotEmpty();
        RuleFor(x => x.PrimerApellido).NotEmpty();

        RuleFor(x => x.CodigoColaborador)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .DebeSerCodigoColaboradorUrlSafe();

        // FechaInicio es REQUERIDA -- el default de DateOnly (0001-01-01) equivale a "no llego"
        // (doctrina bitemporal del BC: el tiempo de los hechos viene del cliente).
        RuleFor(x => x.FechaInicio).NotEqual(default(DateOnly));

        // CA-6: CodigoSede es opcional -- ausente (null) es valido; presente exige un valor no
        // vacio/blanco (NotEmpty tambien rechaza whitespace-only en FluentValidation).
        RuleFor(x => x.CodigoSede).NotEmpty().When(x => x.CodigoSede is not null);
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
