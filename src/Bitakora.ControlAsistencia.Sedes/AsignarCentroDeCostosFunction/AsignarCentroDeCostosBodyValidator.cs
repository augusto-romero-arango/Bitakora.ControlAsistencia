using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction;

// Sin regla de contenido: el CC es opaco, no se valida contra un catalogo.
public class AsignarCentroDeCostosBodyValidator : AbstractValidator<AsignarCentroDeCostosBody>
{
    public AsignarCentroDeCostosBodyValidator()
    {
        RuleFor(x => x.CentroDeCostos).NotEmpty();
    }
}
