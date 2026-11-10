using Bitakora.ControlAsistencia.ControlHoras.Entities;
using FluentValidation;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;

public class RegistrarMarcacionValidator : AbstractValidator<RegistrarMarcacion>
{
    public RegistrarMarcacionValidator()
    {
        // La regla del separador vive en el aggregate, junto al formato que la origina, para no
        // duplicar aqui el literal. Cascade(Stop) evita que el Must evalue un nulo que NotEmpty ya rechazo.
        RuleFor(x => x.CodigoColaborador)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(RegistroDeMarcacionAggregateRoot.EsComponenteValidoDeStreamId)
            .WithMessage("CodigoColaborador no puede contener ':' (separador del identificador de marcacion)");

        RuleFor(x => x.Timestamp).NotEqual(default(DateTime));
    }
}
