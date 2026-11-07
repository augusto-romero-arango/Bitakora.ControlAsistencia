using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction;

public class AsignarModalidadAJornadaBodyValidator : AbstractValidator<AsignarModalidadAJornadaBody>
{
    public AsignarModalidadAJornadaBodyValidator()
    {
        RuleFor(x => x.Modalidad).NotEmpty().Must(m => ModalidadJornada.TryDesde(m, out _));
    }
}
