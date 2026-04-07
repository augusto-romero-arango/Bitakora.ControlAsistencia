using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;

public class SolicitarProgramacionTurnoValidator
    : AbstractValidator<SolicitarProgramacionTurno>
{
    public SolicitarProgramacionTurnoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TurnoId).NotEmpty();

        RuleFor(x => x.Empleado.EmpleadoId).NotEmpty();
        RuleFor(x => x.Empleado.TipoIdentificacion).NotEmpty();
        RuleFor(x => x.Empleado.NumeroIdentificacion).NotEmpty();
        RuleFor(x => x.Empleado.Nombres).NotEmpty();
        RuleFor(x => x.Empleado.Apellidos).NotEmpty();

        RuleFor(x => x.Fechas).NotEmpty();
    }
}
