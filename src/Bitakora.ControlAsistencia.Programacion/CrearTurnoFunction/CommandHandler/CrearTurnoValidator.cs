using FluentValidation;
using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

// Se auto-registra via AddValidatorsFromAssemblyContaining (Program.cs).
public partial class CrearTurnoValidator : AbstractValidator<ComandoCrearTurno>
{
    public CrearTurnoValidator()
    {
        RuleFor(x => x.TurnoId).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty();

        // La marca de descanso y las franjas ordinarias se excluyen mutuamente.
        When(x => x.EsDescanso,
                () => RuleFor(x => x.Ordinarias).Empty()
                    .WithMessage(Mensajes.EsDescansoConFranjas))
            .Otherwise(
                () => RuleFor(x => x.Ordinarias).NotEmpty());
    }
}
