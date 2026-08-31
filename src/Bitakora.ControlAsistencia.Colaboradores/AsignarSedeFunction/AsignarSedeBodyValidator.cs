using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// Solo forma del body (MEF-ADR-0004 capa 1 -> 400). Deliberadamente NO valida charset ni existencia
// o actividad contra el maestro de Sedes: el servidor nunca lo consulta (el filtro de sedes activas
// es del cliente).
// Se descubre via el AddValidatorsFromAssemblyContaining de ComposicionServicios: no se registra en DI.
public class AsignarSedeBodyValidator : AbstractValidator<AsignarSedeBody>
{
    public AsignarSedeBodyValidator()
    {
        RuleFor(x => x.CodigoSede).NotEmpty();
    }
}
