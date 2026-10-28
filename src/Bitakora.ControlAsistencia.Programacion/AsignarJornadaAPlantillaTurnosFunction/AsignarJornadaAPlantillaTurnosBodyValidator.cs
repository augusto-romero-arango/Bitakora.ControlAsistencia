using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaTurnosFunction;

public class AsignarJornadaAPlantillaTurnosBodyValidator : AbstractValidator<AsignarJornadaAPlantillaTurnosBody>
{
    public AsignarJornadaAPlantillaTurnosBodyValidator() =>
        RuleFor(x => x.JornadaId).NotEmpty();
}
