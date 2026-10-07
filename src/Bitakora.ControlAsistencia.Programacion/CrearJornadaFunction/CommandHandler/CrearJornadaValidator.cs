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
    }
}
