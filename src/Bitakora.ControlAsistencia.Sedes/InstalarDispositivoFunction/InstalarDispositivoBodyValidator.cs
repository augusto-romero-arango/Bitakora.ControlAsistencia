using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;

// El DispositivoId es segmento de ruta del DELETE: comparte el charset URL-safe del codigo de sede
// (MEF-ADR-0043 seccion 1.1) y se rechaza sin normalizar, porque lo asigna un tercero. Mensaje propio:
// el del helper habla del "codigo de la sede".
public class InstalarDispositivoBodyValidator : AbstractValidator<InstalarDispositivoBody>
{
    private const string MensajeDispositivoIdUrlSafe =
        "El identificador del dispositivo solo admite letras sin tilde, digitos y los caracteres - . _ ~";

    public InstalarDispositivoBodyValidator()
    {
        RuleFor(x => x.DispositivoId)
            .NotEmpty()
            .DebeSerCodigoSedeUrlSafe()
            .WithMessage(MensajeDispositivoIdUrlSafe);
    }
}
