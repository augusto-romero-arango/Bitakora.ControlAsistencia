using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

public partial class AgregarSubFranjaBodyValidator : AbstractValidator<AgregarSubFranjaBody>
{
    public AgregarSubFranjaBodyValidator() =>
        RuleFor(x => x.Tipo)
            .Must(tipo => Enum.TryParse<TipoSubFranja>(tipo, ignoreCase: true, out _))
            .WithMessage(Mensajes.TipoDesconocido);
}
