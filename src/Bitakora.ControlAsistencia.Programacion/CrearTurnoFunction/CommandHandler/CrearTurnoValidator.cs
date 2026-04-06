using FluentValidation;
using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

// HU-4: Validacion de estructura del request antes del command handler
// CA-5: TurnoId no vacio, Nombre no vacio, Ordinarias no vacia
// CA-6: se auto-registra via AddValidatorsFromAssemblyContaining (configurado en Program.cs)
public class CrearTurnoValidator : AbstractValidator<ComandoCrearTurno>
{
    public CrearTurnoValidator()
    {
        RuleFor(x => x.TurnoId).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty();
        RuleFor(x => x.Ordinarias).NotEmpty();
    }
}
