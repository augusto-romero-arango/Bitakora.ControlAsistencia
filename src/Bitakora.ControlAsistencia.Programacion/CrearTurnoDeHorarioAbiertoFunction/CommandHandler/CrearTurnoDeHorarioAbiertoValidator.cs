using FluentValidation;
using Comando = Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CrearTurnoDeHorarioAbierto;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CommandHandler;

public class CrearTurnoDeHorarioAbiertoValidator : AbstractValidator<Comando>
{
    public CrearTurnoDeHorarioAbiertoValidator()
    {
        RuleFor(x => x.TurnoId).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty();
        RuleFor(x => x.Duracion).NotNull();
    }
}
