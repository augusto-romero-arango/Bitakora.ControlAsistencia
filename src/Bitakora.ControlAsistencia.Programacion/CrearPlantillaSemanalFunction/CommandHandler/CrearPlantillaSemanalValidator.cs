using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;

// Se auto-registra via AddValidatorsFromAssemblyContaining (Program.cs).
// El rango de Semanas NO se valida aqui: es invariante de dominio y vive en el factory del evento
// (PlantillaSemanalCreada.Crear). Duplicarlo aqui crearia dos fuentes del mismo literal.
public class CrearPlantillaSemanalValidator : AbstractValidator<CrearPlantillaSemanal>
{
    public CrearPlantillaSemanalValidator()
    {
        RuleFor(x => x.PlantillaId).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty();
        RuleFor(x => x.JornadaId).NotEqual(Guid.Empty).When(x => x.JornadaId.HasValue);
    }
}
