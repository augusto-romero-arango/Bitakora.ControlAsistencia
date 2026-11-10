// Guardrail: una clave perdida del .resx hace que Mensajes.X resuelva null y los WithMessage($"*{X}*")
// de AprobarDiaCommandHandlerTests pasen en falso.

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
        AprobarDiaCommandHandler.Mensajes.MarcacionesAmbiguasSinResolver.Should().NotBeNullOrWhiteSpace();
    }
}
