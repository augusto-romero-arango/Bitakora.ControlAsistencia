using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using FluentValidation;
using Comando = Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CrearJornada;

namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CommandHandler;

public class CrearJornadaValidator : AbstractValidator<Comando>
{
    public CrearJornadaValidator()
    {
        RuleFor(x => x.JornadaId).NotEmpty();
        RuleFor(x => x.HorasSemanales).NotNull();
        RuleFor(x => x.TopeDiario).NotNull();
        RuleFor(x => x.MinimoDiario).NotNull();
        RuleFor(x => x.Modalidad)
            .Must(m => ModalidadJornada.TryDesde(m, out _))
            .When(x => !string.IsNullOrEmpty(x.Modalidad));
    }
}
