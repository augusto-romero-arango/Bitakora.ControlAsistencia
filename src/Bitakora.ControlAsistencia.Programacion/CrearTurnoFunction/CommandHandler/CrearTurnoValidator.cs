using FluentValidation;
using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

public partial class CrearTurnoValidator : AbstractValidator<ComandoCrearTurno>
{
    public CrearTurnoValidator()
    {
        RuleFor(x => x.TurnoId).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty();

        // Marca de descanso y franjas se excluyen mutuamente; sin la marca, Ordinarias vacia es valida
        // (un turno nace vacio y se completa por pasos, CA-ADR-0033).
        When(x => x.EsDescanso,
            () => RuleFor(x => x.Ordinarias).Empty()
                .WithMessage(Mensajes.EsDescansoConFranjas));
    }
}
