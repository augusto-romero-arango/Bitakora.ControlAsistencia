using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;

public class RegistrarColaboradorValidator : AbstractValidator<RegistrarColaborador>
{
    public RegistrarColaboradorValidator()
    {
        // "cc" (minusculas) debe seguir siendo valido: la normalizacion vive en
        // TipoIdentificacion.Desde, no en este borde.
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

        // El default de DateOnly (0001-01-01) equivale a "no llego".
        RuleFor(x => x.FechaInicio).NotEqual(default(DateOnly));

        RuleFor(x => x.CodigoSede).NotEmpty().When(x => x.CodigoSede is not null);
    }

    // Un codigo fuera de la lista debe ser error de validacion (400), no una excepcion no controlada.
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
