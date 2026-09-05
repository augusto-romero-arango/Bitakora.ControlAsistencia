using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction;

// Se auto-registra via AddValidatorsFromAssemblyContaining (Program.cs). NotEmpty en TurnoId --
// mismo criterio que AsignarEtiquetaBodyValidator (Colaboradores).
public class AsignarTurnoADiaDePlantillaSemanalBodyValidator
    : AbstractValidator<AsignarTurnoADiaDePlantillaSemanalBody>
{
    public AsignarTurnoADiaDePlantillaSemanalBodyValidator() =>
        RuleFor(x => x.TurnoId).NotEmpty();
}
