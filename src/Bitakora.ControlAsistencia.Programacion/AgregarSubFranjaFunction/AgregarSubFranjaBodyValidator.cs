using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

// Se auto-registra via AddValidatorsFromAssemblyContaining (Program.cs).
public partial class AgregarSubFranjaBodyValidator : AbstractValidator<AgregarSubFranjaBody>;
