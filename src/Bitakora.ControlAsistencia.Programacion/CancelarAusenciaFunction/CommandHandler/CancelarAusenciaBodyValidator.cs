using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction.CommandHandler;

public class CancelarAusenciaBodyValidator : AbstractValidator<CancelarAusenciaBody>
{
    public CancelarAusenciaBodyValidator()
    {
        RuleFor(x => x.Fechas).NotEmpty();
    }
}
