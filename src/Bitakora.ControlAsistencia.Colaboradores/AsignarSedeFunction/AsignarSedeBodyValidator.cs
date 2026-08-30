using FluentValidation;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// Issue #465 (MEF-ADR-0043 paso 2): validacion de forma del body reducido en el borde (MEF-ADR-0004
// capa 1 -> 400 BadRequest). NO valida charset ni existencia/actividad contra el maestro de Sedes
// (decision de refinamiento: el filtro de sedes activas es del cliente/UI, el servidor nunca
// consulta el maestro).
// Se descubre via el AddValidatorsFromAssemblyContaining que ComposicionServicios ya configura: no
// requiere tocar el wiring de DI.
public class AsignarSedeBodyValidator : AbstractValidator<AsignarSedeBody>
{
    public AsignarSedeBodyValidator()
    {
        RuleFor(x => x.CodigoSede).NotEmpty();
    }
}
