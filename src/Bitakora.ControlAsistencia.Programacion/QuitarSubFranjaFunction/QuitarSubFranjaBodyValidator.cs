using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Se auto-registra via AddValidatorsFromAssemblyContaining (Program.cs). Reusa el mensaje de
// AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido (#603) -- misma regla de discriminador
// Tipo, sin duplicar el .resx (MEF-ADR-0018).
public partial class QuitarSubFranjaBodyValidator : AbstractValidator<QuitarSubFranjaBody>;
