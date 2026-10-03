using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using FluentValidation;

namespace Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction.CommandHandler;

public class ProgramarAusenciaBodyValidator : AbstractValidator<ProgramarAusenciaBody>
{
    public ProgramarAusenciaBodyValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Identificacion).NotEmpty();
        RuleFor(x => x.NombreCompleto).NotEmpty();
        RuleFor(x => x.FechaInicio).NotEmpty();
        RuleFor(x => x.FechaFin).NotEmpty().GreaterThanOrEqualTo(x => x.FechaInicio);
        RuleFor(x => x.Motivo).NotEmpty().Must(m => MotivoAusencia.TryDesde(m, out _));
    }
}
