using Bitakora.ControlAsistencia.ControlHoras.Entities;
using FluentValidation;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;

// Issue #279: validacion de forma del comando RegistrarMarcacion en el borde.
// CA-1: descubierto por AddValidatorsFromAssemblyContaining ya registrado en
// ComposicionServicios (no requiere tocar el wiring de DI).
public class RegistrarMarcacionValidator : AbstractValidator<RegistrarMarcacion>
{
    public RegistrarMarcacionValidator()
    {
        // CA-2: CodigoColaborador nulo, vacio o solo espacios en blanco produce 400.
        // CA-3: CodigoColaborador con el separador del stream ID produce 400. La regla vive en el aggregate,
        // junto al formato que la origina (RegistroDeMarcacionAggregateRoot.ComputarStreamId), para no
        // duplicar aqui el literal del separador.
        // Cascade(Stop) evita que el Must evalue un CodigoColaborador nulo que NotEmpty ya rechazo.
        RuleFor(x => x.CodigoColaborador)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(RegistroDeMarcacionAggregateRoot.EsComponenteValidoDeStreamId)
            .WithMessage("CodigoColaborador no puede contener ':' (separador del identificador de marcacion)");

        // CA-4: Timestamp con el valor default de DateTime produce 400.
        RuleFor(x => x.Timestamp).NotEqual(default(DateTime));
    }
}
