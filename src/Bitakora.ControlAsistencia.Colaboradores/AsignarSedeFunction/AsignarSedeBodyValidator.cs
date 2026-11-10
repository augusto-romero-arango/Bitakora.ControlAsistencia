using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// Deliberadamente NO valida charset ni existencia o actividad contra el maestro de Sedes: el
// servidor nunca lo consulta (el filtro de sedes activas es del cliente).
public class AsignarSedeBodyValidator : AbstractValidator<AsignarSedeBody>
{
    public AsignarSedeBodyValidator()
    {
        RuleFor(x => x.CodigoSede).NotEmpty();
    }
}
