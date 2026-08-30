using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.CancelarProgramacionFunction.CommandHandler;

public class CancelarProgramacionValidator : AbstractValidator<CancelarProgramacion>
{
    public CancelarProgramacionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Colaborador).NotNull();
        When(x => x.Colaborador is not null, () =>
        {
            RuleFor(x => x.Colaborador.Identificacion).NotEmpty();
            RuleFor(x => x.Colaborador.CodigoColaborador).NotEmpty();
            RuleFor(x => x.Colaborador.NombreCompleto).NotEmpty();
        });

        RuleFor(x => x.Fechas).NotEmpty();
        RuleFor(x => x.Fechas)
            .Must(fechas => fechas.Distinct().Count() == fechas.Count)
            .WithMessage("Las fechas no pueden estar duplicadas")
            .When(x => x.Fechas.Count > 0);
    }
}
