using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction;

public class AsignarJornadaAPlantillaSemanalBodyValidator
    : AbstractValidator<AsignarJornadaAPlantillaSemanalBody>
{
    public AsignarJornadaAPlantillaSemanalBodyValidator() =>
        RuleFor(x => x.JornadaId).NotEmpty();
}
