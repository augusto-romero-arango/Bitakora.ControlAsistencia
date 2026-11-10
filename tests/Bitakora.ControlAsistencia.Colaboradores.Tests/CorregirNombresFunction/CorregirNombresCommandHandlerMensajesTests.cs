// Por que existe este guardrail: ver TerminarVinculacionCommandHandlerMensajesTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction;

public class CorregirNombresCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenACorregirNombresCommandHandler()
    {
        CorregirNombresCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
    }
}
