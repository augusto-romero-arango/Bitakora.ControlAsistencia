using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction.CommandHandler;

public partial class DescartarBorradorDeProgramacionBodyValidator : AbstractValidator<DescartarBorradorDeProgramacionBody>
{
    private const int MaximoDiasDelRango = 366;

    public DescartarBorradorDeProgramacionBodyValidator()
    {
        RuleFor(x => x.Desde).NotNull().WithMessage(Mensajes.DesdeRequerido);
        RuleFor(x => x.Hasta).NotNull().WithMessage(Mensajes.HastaRequerido);

        When(x => x.Desde is not null && x.Hasta is not null, () =>
        {
            RuleFor(x => x).Must(x => x.Desde <= x.Hasta)
                .WithMessage(Mensajes.DesdeMayorQueHasta);
            RuleFor(x => x).Must(x => x.Hasta!.Value.DayNumber - x.Desde!.Value.DayNumber + 1 <= MaximoDiasDelRango)
                .WithMessage(Mensajes.RangoExcedeElMaximo);
        });
    }
}
