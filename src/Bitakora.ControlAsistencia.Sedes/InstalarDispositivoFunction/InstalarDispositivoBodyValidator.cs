using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;

// Mismo charset URL-safe que CodigoSede (MEF-ADR-0043 seccion 1.3): DispositivoId se expone luego
// como segmento de ruta en el DELETE, asi que reusa la regla compartida de ValidacionesCompartidasSedes.
public class InstalarDispositivoBodyValidator : AbstractValidator<InstalarDispositivoBody>
{
    public InstalarDispositivoBodyValidator()
    {
        RuleFor(x => x.DispositivoId).NotEmpty().DebeSerCodigoSedeUrlSafe();
    }
}
