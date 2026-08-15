using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;

// Issue #355: validacion de forma del comando AsignarEtiqueta en el borde (MEF-ADR-0004 capa 1 ->
// 400 BadRequest). CA-7: TipoIdentificacion (requerido + en la lista cerrada), NumeroIdentificacion,
// Categoria y Valor requeridos -- las invariantes de doble forma/normalizacion viven en el VO
// Etiqueta (#353), no en este validator (el aggregate solo gobierna el diccionario).
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI. Precedente: AnularTerminacionValidator (issue #354).
public class AsignarEtiquetaValidator : AbstractValidator<AsignarEtiqueta>
{
    public AsignarEtiquetaValidator()
    {
        // Cascade(Stop) evita que Must evalue un valor vacio que NotEmpty ya rechazo -- mismo
        // criterio que CorregirFechaInicioVinculacionValidator/TerminarVinculacionValidator.
        RuleFor(x => x.TipoIdentificacion)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(EsTipoIdentificacionReconocido)
            .WithMessage("El tipo de identificacion no es uno de los reconocidos");

        RuleFor(x => x.NumeroIdentificacion).NotEmpty();
        RuleFor(x => x.Categoria).NotEmpty();
        RuleFor(x => x.Valor).NotEmpty();
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
