// Por que existe este guardrail: ver TerminarVinculacionCommandHandlerMensajesTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RetirarEtiquetaFunction;

public class RetirarEtiquetaCommandHandlerMensajesTests
{
    [Fact]
    public void Mensajes_ResuelvenTextoNoVacio_CuandoPertenecenARetirarEtiquetaCommandHandler()
    {
        RetirarEtiquetaCommandHandler.Mensajes.ColaboradorNoEncontrado.Should().NotBeNullOrWhiteSpace();
        RetirarEtiquetaCommandHandler.Mensajes.VinculacionTerminada.Should().NotBeNullOrWhiteSpace();
    }
}
