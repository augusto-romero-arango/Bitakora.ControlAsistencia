using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Se auto-registra via AddValidatorsFromAssemblyContaining (Program.cs). Reusa el mensaje de
// AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido (#603) -- misma regla de discriminador
// Tipo, sin duplicar el .resx (MEF-ADR-0018).
public partial class QuitarSubFranjaBodyValidator : AbstractValidator<QuitarSubFranjaBody>
{
    public QuitarSubFranjaBodyValidator() =>
        RuleFor(x => x.Tipo)
            .Must(tipo => Enum.TryParse<TipoSubFranja>(tipo, ignoreCase: true, out _))
            .WithMessage(AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido);
}
