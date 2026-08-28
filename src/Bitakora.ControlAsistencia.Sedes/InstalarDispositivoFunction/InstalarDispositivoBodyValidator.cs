using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;

// El DispositivoId se expone luego como segmento de ruta en el DELETE, asi que comparte el charset
// URL-safe del CodigoSede (RFC 3986 unreserved, MEF-ADR-0043 seccion 1.1) y se rechaza con 400, sin
// normalizar (seccion 1.2: identificador asignado por un tercero). El mensaje si es propio: el del
// helper compartido habla del "codigo de la sede" y senalaria al caller un campo que no fallo.
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
