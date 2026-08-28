using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction;

// CA-5: CC vacio -> 400 (forma en el borde, MEF-ADR-0004 capa 1). Sin regla de contenido: el CC es
// opaco, nadie lo interpreta ni lo valida contra un catalogo.
public class AsignarCentroDeCostosBodyValidator : AbstractValidator<AsignarCentroDeCostosBody>
{
    public AsignarCentroDeCostosBodyValidator()
    {
        RuleFor(x => x.CentroDeCostos).NotEmpty();
    }
}
