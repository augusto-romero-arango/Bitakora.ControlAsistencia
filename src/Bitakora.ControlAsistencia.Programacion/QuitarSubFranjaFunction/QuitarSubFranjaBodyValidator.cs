using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Reusa el .resx del validator de AgregarSubFranja: misma regla sobre el mismo discriminador.
public class QuitarSubFranjaBodyValidator : AbstractValidator<QuitarSubFranjaBody>
{
    public QuitarSubFranjaBodyValidator() =>
        RuleFor(x => x.Tipo)
            .Must(tipo => Enum.TryParse<TipoSubFranja>(tipo, ignoreCase: true, out _))
            .WithMessage(AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido);
}
