using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Se auto-registra via AddValidatorsFromAssemblyContaining (Program.cs). Reusa el .resx del
// validator de AgregarSubFranja: misma regla sobre el mismo discriminador (MEF-ADR-0018).
public class QuitarSubFranjaBodyValidator : AbstractValidator<QuitarSubFranjaBody>
{
    public QuitarSubFranjaBodyValidator() =>
        RuleFor(x => x.Tipo)
            .Must(tipo => Enum.TryParse<TipoSubFranja>(tipo, ignoreCase: true, out _))
            .WithMessage(AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido);
}
