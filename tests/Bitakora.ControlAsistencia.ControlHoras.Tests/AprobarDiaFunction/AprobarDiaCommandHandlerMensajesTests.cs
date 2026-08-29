// Issue #489: guardrail de resolucion del .resx del handler (MEF-ADR-0009), mismo patron que
// TerminarVinculacionCommandHandlerMensajesTests (Colaboradores). Sin este guardrail, una clave
// perdida del .resx haria que Mensajes.X resuelva null en runtime y los WithMessage($"*{X}*") de
// AprobarDiaCommandHandlerTests pasen en falso (con X == null, "*{null}*" matchea cualquier
// mensaje).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AprobarDiaFunction;

public class AprobarDiaCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenAAprobarDiaCommandHandler()
    {
        AprobarDiaCommandHandler.Mensajes.ConflictosSinDecidir.Should().NotBeNullOrWhiteSpace();
        AprobarDiaCommandHandler.Mensajes.CodigoSedeNoCandidata.Should().NotBeNullOrWhiteSpace();
        AprobarDiaCommandHandler.Mensajes.DecisionParaFranjaInvalida.Should().NotBeNullOrWhiteSpace();
        AprobarDiaCommandHandler.Mensajes.DiaYaAprobado.Should().NotBeNullOrWhiteSpace();
    }
}
