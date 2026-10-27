using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction;

public class AsignarTurnoADiaDePlantillaTurnosBodyValidator
    : AbstractValidator<AsignarTurnoADiaDePlantillaTurnosBody>
{
    public AsignarTurnoADiaDePlantillaTurnosBodyValidator() =>
        RuleFor(x => x.TurnoId).NotEmpty();
}
