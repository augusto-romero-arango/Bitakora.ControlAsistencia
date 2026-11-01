using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

public class SolicitarProgramacionTurnoValidator
    : AbstractValidator<SolicitarProgramacionTurno>
{
    public SolicitarProgramacionTurnoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TurnoId).NotEmpty();

        RuleFor(x => x.Colaborador).NotNull();
        When(x => x.Colaborador is not null, () =>
        {
            RuleFor(x => x.Colaborador.Identificacion).NotEmpty();
            RuleFor(x => x.Colaborador.CodigoColaborador).NotEmpty();
            RuleFor(x => x.Colaborador.NombreCompleto).NotEmpty();
        });

        RuleFor(x => x.Fechas).NotEmpty();

        RuleFor(x => x.JornadaId).NotEqual(Guid.Empty);

        When(x => x.Sede is not null, () =>
        {
            RuleFor(x => x.Sede!.Id).NotEmpty();
            RuleFor(x => x.Sede!.Nombre).NotEmpty();
        });
    }
}
