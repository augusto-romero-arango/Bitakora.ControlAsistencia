using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;

// Se auto-registra via AddValidatorsFromAssemblyContaining (Program.cs). El rango de Semanas es
// invariante de dominio y vive en el factory del evento (PlantillaSemanalCreada.Crear) -- dos
// fuentes del mismo literal es lo que se descarta aqui (ver investigacion del planner, issue #620).
// CA-5: NotEmpty en PlantillaId y Nombre.
public partial class CrearPlantillaSemanalValidator : AbstractValidator<CrearPlantillaSemanal>
{
    public CrearPlantillaSemanalValidator() => throw new NotImplementedException();
}
