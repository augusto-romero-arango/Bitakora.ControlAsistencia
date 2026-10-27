using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler;

public class CrearPlantillaTurnosValidator : AbstractValidator<CrearPlantillaTurnos>
{
    public CrearPlantillaTurnosValidator()
    {
        RuleFor(x => x.PlantillaId).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty();
        RuleFor(x => x.JornadaId).NotEqual(Guid.Empty).When(x => x.JornadaId.HasValue);
    }
}
