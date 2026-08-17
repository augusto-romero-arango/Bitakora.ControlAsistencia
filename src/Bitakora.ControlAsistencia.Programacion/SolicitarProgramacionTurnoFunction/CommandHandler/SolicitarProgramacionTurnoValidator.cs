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
            RuleFor(x => x.Colaborador.CodigoColaborador).NotEmpty();
            RuleFor(x => x.Colaborador.TipoIdentificacion).NotEmpty();
            RuleFor(x => x.Colaborador.NumeroIdentificacion).NotEmpty();
            RuleFor(x => x.Colaborador.Nombres).NotEmpty();
            RuleFor(x => x.Colaborador.Apellidos).NotEmpty();
        });

        RuleFor(x => x.Fechas).NotEmpty();

        // Issue #331 CA-3: sede es opcional (null = sin sede asignada), pero cuando el objeto
        // viene presente, sus propiedades Id y Nombre son obligatorias y no vacias.
        When(x => x.Sede is not null, () =>
        {
            RuleFor(x => x.Sede!.Id).NotEmpty();
            RuleFor(x => x.Sede!.Nombre).NotEmpty();
        });
    }
}
